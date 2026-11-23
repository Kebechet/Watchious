"use strict";

// The inspector's panels. preview.js mirrors the app's screen and calls these hooks; it loads after this file.

const MaxLogEntries = 300;
const statusTexts = {
    connected: "connected",
    reset: "the app started again",
    waiting: "waiting for the app",
};

window.watchiousInspector = {
    onBatch(byteCount, operations) {
        addLogEntry("patch", `${operations.length} patches, ${byteCount} bytes`, operations);
        renderTree();
    },
    onEvent(nativeEvent) {
        const value = nativeEvent.value === null || nativeEvent.value === undefined ? "" : ` = ${JSON.stringify(nativeEvent.value)}`;
        addLogEntry("event", `${nativeEvent.name} (handler ${nativeEvent.id})${value}`);
    },
    onError(error) {
        addLogEntry("error", error.split("\n")[0], [error]);
    },
    onStatus(status) {
        document.getElementById("status").textContent = statusTexts[status] ?? status;
        if (status === "reset") {
            renderTree();
        }
    },
};

function renderTree() {
    const lines = [];
    for (const child of window.watchiousPreview.root.children) {
        describe(child, 0, lines);
    }
    document.getElementById("tree").textContent = lines.join("\n");
}

function describe(node, depth, lines) {
    const indent = "  ".repeat(depth);
    if (node.type === "#text") {
        if ((node.text ?? "").trim() !== "") {
            lines.push(`${indent}"${node.text}"  #${node.id}`);
        }
        return;
    }

    const attributes = [...node.attributes].map(([name, value]) => `${name}=${JSON.stringify(value)}`);
    const events = [...node.events].map(([name, handler]) => `on${name}=${handler}`);
    lines.push(`${indent}<${node.type}> #${node.id} ${[...attributes, ...events].join(" ")}`.trimEnd());
    for (const child of node.children) {
        describe(child, depth + 1, lines);
    }
}

function addLogEntry(kind, title, details) {
    const log = document.getElementById("log");
    const entry = document.createElement(details?.length ? "details" : "div");
    entry.className = `entry ${kind}`;
    const summary = document.createElement(details?.length ? "summary" : "span");
    summary.textContent = `${new Date().toLocaleTimeString()}  ${kind}  ${title}`;
    entry.append(summary);
    if (details?.length) {
        const list = document.createElement("pre");
        list.textContent = details.join("\n");
        entry.append(list);
    }
    log.prepend(entry);
    while (log.childElementCount > MaxLogEntries) {
        log.lastElementChild.remove();
    }
}
