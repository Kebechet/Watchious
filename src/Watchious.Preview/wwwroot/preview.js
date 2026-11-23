"use strict";

// A third Watchious host, next to SwiftUI and Compose: it applies the same protocol v2 patches (see Watchious.Core's
// Protocol/PatchOperation.cs) to a node tree, and keeps one DOM element per node. The preview page runs an app of its
// own; the inspector page (inspector.html) mirrors an app running on a watch and reports to inspector.js.

const platform = new URLSearchParams(location.search).get("platform") === "watchos" ? "watchos" : "wearos";
const isInspector = document.body.dataset.mode === "inspector";
const hooks = window.watchiousInspector ?? {};
const defaultSpacing = platform === "watchos" ? 8 : 4;
document.getElementById(platform).classList.add("active");
const watch = document.getElementById("watch");
watch.classList.add(platform);
const errorView = document.getElementById("error");
const decoder = new TextDecoder();

const root = newNode(0, "#root", document.getElementById("screen"));
const nodes = new Map([[0, root]]);
const changedNodes = new Set();
window.watchiousPreview = { root, nodes };

const sessionPath = isInspector ? "/inspector/page" : `/session?platform=${platform}`;
const socket = new WebSocket(`${location.protocol === "https:" ? "wss" : "ws"}://${location.host}${sessionPath}`);
socket.binaryType = "arraybuffer";
socket.onmessage = (message) => {
    if (typeof message.data === "string") {
        receiveText(JSON.parse(message.data));
        return;
    }
    applyPatches(message.data);
    // The app keeps the event handlers this batch replaced until the page confirms it (PreviewSession.cs). The
    // inspector's app is confirmed by its own watch.
    if (!isInspector) {
        socket.send(JSON.stringify({ applied: true }));
    }
};
socket.onclose = () => showError(isInspector
    ? "The inspector stopped."
    : "The app stopped. Reload the page to start it again.");

// Errors from the app; and for the inspector, the events the watch dispatched and the app's connection status.
function receiveText(message) {
    if (message.error) {
        showError(message.error);
        hooks.onError?.(message.error);
    } else if (message.status) {
        if (message.status === "reset") {
            reset();
        }
        hooks.onStatus?.(message.status);
    } else if (message.name) {
        hooks.onEvent?.(message);
    }
}

// The watch app started again: its first batch builds the tree from nothing.
function reset() {
    nodes.clear();
    nodes.set(0, root);
    root.children = [];
    root.element.replaceChildren();
    errorView.hidden = true;
}

document.addEventListener("keydown", (keyEvent) => {
    if (keyEvent.key === "Escape") {
        const stack = [...nodes.values()].find((node) => node.type === "NavigationStack");
        const pages = stack ? stack.children.filter((child) => child.type === "Page") : [];
        if (pages.length > 1) {
            send(stack, "back", String(pages.length - 1));
        }
    }
});

function showError(message) {
    errorView.textContent = message;
    errorView.hidden = false;
}

function send(node, name, value) {
    const id = node.events.get(name);
    if (id === undefined || socket.readyState !== WebSocket.OPEN) {
        return;
    }
    socket.send(JSON.stringify({ id, name, value: value ?? null }));
}

function newNode(id, type, element) {
    return { id, type, element, attributes: new Map(), events: new Map(), children: [], text: null, parent: null };
}

function applyPatches(buffer) {
    const view = new DataView(buffer);
    let position = 0;
    const int32 = () => {
        const value = view.getInt32(position, true);
        position += 4;
        return value;
    };
    const uint32 = () => {
        const value = view.getUint32(position, true);
        position += 4;
        return value;
    };
    const uint64 = () => {
        const value = view.getBigUint64(position, true);
        position += 8;
        return Number(value);
    };
    const string = () => {
        const length = int32();
        if (length < 0) {
            return null;
        }
        const value = decoder.decode(new Uint8Array(buffer, position, length));
        position += length;
        return value;
    };

    const operations = isInspector ? [] : null;
    const version = int32();
    if (version !== 2) {
        showError(`This page supports Watchious patch protocol 2, not ${version}.`);
        return;
    }

    while (position < buffer.byteLength) {
        const operation = view.getUint8(position++);
        const id = uint32();
        switch (operation) {
            case 1: {
                const node = create(id, string());
                operations?.push(`create ${node.type} #${id}`);
                break;
            }
            case 2: {
                const node = create(id, "#text");
                node.text = string();
                operations?.push(`create text #${id} ${JSON.stringify(node.text)}`);
                break;
            }
            case 3: {
                const node = nodes.get(id);
                const name = string();
                const value = string();
                if (value === null) {
                    node.attributes.delete(name);
                } else {
                    node.attributes.set(name, value);
                }
                operations?.push(`#${id} ${name} = ${JSON.stringify(value)}`);
                markChanged(node);
                break;
            }
            case 4: {
                const node = nodes.get(id);
                const name = string();
                node.attributes.delete(name);
                operations?.push(`#${id} remove ${name}`);
                markChanged(node);
                break;
            }
            case 5: {
                const node = nodes.get(id);
                const name = string();
                const handler = uint64();
                node.events.set(name, handler);
                operations?.push(`#${id} on ${name} -> handler ${handler}`);
                break;
            }
            case 6: {
                const name = string();
                nodes.get(id).events.delete(name);
                operations?.push(`#${id} remove on ${name}`);
                break;
            }
            case 7: {
                const node = nodes.get(id);
                node.text = string();
                operations?.push(`#${id} text = ${JSON.stringify(node.text)}`);
                markChanged(node);
                break;
            }
            case 8: {
                const parent = nodes.get(id);
                const index = int32();
                const child = nodes.get(uint32());
                parent.children.splice(index, 0, child);
                child.parent = parent;
                operations?.push(`insert #${child.id} into #${id} at ${index}`);
                markChanged(parent);
                break;
            }
            case 9: {
                const parent = nodes.get(id);
                const index = int32();
                const [child] = parent.children.splice(index, 1);
                forget(child);
                operations?.push(`remove #${child.id} from #${id} at ${index}`);
                markChanged(parent);
                break;
            }
            case 10: {
                const parent = nodes.get(id);
                const fromIndex = int32();
                const toIndex = int32();
                const [child] = parent.children.splice(fromIndex, 1);
                parent.children.splice(toIndex, 0, child);
                operations?.push(`move #${child.id} in #${id} from ${fromIndex} to ${toIndex}`);
                markChanged(parent);
                break;
            }
            default:
                showError(`Unknown Watchious patch operation ${operation}.`);
                return;
        }
    }

    for (const node of changedNodes) {
        render(node);
    }
    changedNodes.clear();
    hooks.onBatch?.(buffer.byteLength, operations);
}

function create(id, type) {
    const node = newNode(id, type, null);
    node.element = createElement(node);
    nodes.set(id, node);
    markChanged(node);
    return node;
}

// A Label shows the text inside it, so a change to that text redraws the Label.
function markChanged(node) {
    changedNodes.add(node);
    if (node.type === "#text" && node.parent) {
        changedNodes.add(node.parent);
    }
}

function forget(node) {
    nodes.delete(node.id);
    node.children.forEach(forget);
}

function element(tag, className) {
    const created = document.createElement(tag);
    if (className) {
        created.className = className;
    }
    return created;
}

function createElement(node) {
    switch (node.type) {
        case "#text":
            return document.createTextNode("");
        case "NavigationStack": {
            const stack = element("div", "navigation-stack");
            const back = element("button", "back-button");
            back.textContent = "‹";
            back.onclick = () => send(node, "back", String(pages(node).length - 1));
            stack.back = back;
            return stack;
        }
        case "Page":
            return element("div", "page");
        case "ScrollView":
            return element("div", "scroll-view");
        case "List":
            return element("div", "list");
        case "VStack":
            return element("div", "vstack");
        case "HStack":
            return element("div", "hstack");
        case "Label":
            return element("div", "label");
        case "Button": {
            const button = element("button", "button");
            button.onclick = () => send(node, "click");
            return button;
        }
        case "Toggle": {
            const toggle = element("label", "toggle");
            toggle.text = element("span");
            toggle.input = element("input");
            toggle.input.type = "checkbox";
            toggle.input.onchange = () => send(node, "toggled", toggle.input.checked ? "true" : "false");
            toggle.append(toggle.text, toggle.input);
            return toggle;
        }
        case "ProgressView":
            return element("div", "progress-view");
        case "Picker": {
            const picker = element("label", "picker");
            picker.text = element("span");
            picker.select = element("select");
            picker.select.onchange = () => send(node, "selected", String(picker.select.selectedIndex));
            picker.append(picker.text, picker.select);
            return picker;
        }
        case "TextField": {
            const input = element("input", "text-field");
            input.oninput = () => send(node, "changed", input.value);
            return input;
        }
        case "Image":
            return element("img", "image");
        default:
            return element("div", "vstack");
    }
}

function attribute(node, name) {
    return node.attributes.get(name) ?? null;
}

function pages(stack) {
    return stack.children.filter((child) => child.type === "Page");
}

function render(node) {
    const target = node.element;
    switch (node.type) {
        case "#text":
            target.textContent = node.text ?? "";
            return;
        case "NavigationStack": {
            const stackPages = pages(node);
            stackPages.forEach((page, index) => {
                page.element.hidden = index !== stackPages.length - 1;
            });
            target.replaceChildren(...stackPages.map((page) => page.element));
            if (stackPages.length > 1) {
                target.append(target.back);
            }
            return;
        }
        case "List":
            target.replaceChildren(...visibleChildren(node).map((child) => {
                const row = element("div", "row");
                row.append(child.element);
                return row;
            }));
            return;
        case "VStack":
        case "HStack": {
            const spacing = Number(attribute(node, "Spacing") ?? defaultSpacing);
            target.style.gap = `${spacing}px`;
            target.style.alignItems = alignment(attribute(node, "Alignment"));
            target.replaceChildren(...node.children.map((child) => child.element));
            return;
        }
        case "Label":
            target.textContent = node.children.filter((child) => child.type === "#text").map((child) => child.text ?? "").join("");
            target.className = `label ${(attribute(node, "Style") ?? "Body").toLowerCase()}`;
            return;
        case "Button":
            target.textContent = attribute(node, "Text") ?? "";
            target.disabled = attribute(node, "IsEnabled") === "false";
            return;
        case "Toggle":
            target.text.textContent = attribute(node, "Text") ?? "";
            target.input.checked = attribute(node, "IsOn") === "true";
            return;
        case "ProgressView":
            renderProgress(node);
            return;
        case "Picker":
            renderPicker(node);
            return;
        case "TextField": {
            target.placeholder = attribute(node, "Placeholder") ?? "";
            const text = attribute(node, "Text") ?? "";
            if (document.activeElement !== target && target.value !== text) {
                target.value = text;
            }
            return;
        }
        case "Image": {
            target.src = `/images/${encodeURIComponent(attribute(node, "Source") ?? "")}`;
            target.alt = attribute(node, "Description") ?? "";
            const width = attribute(node, "Width");
            const height = attribute(node, "Height");
            target.style.width = width ? `${width}px` : "";
            target.style.height = height ? `${height}px` : "";
            return;
        }
        default:
            target.replaceChildren(...node.children.map((child) => child.element));
    }
}

// Blank text is the whitespace between controls in the Razor markup.
function visibleChildren(node) {
    return node.children.filter((child) => child.type !== "#text" || (child.text ?? "").trim() !== "");
}

function alignment(value) {
    switch (value) {
        case "Leading":
        case "Top":
            return "flex-start";
        case "Trailing":
        case "Bottom":
            return "flex-end";
        default:
            return "center";
    }
}

function renderProgress(node) {
    const target = node.element;
    const text = attribute(node, "Text");
    const value = attribute(node, "Value");
    const parts = [];
    if (text) {
        const label = element("span", "label caption");
        label.textContent = text;
        parts.push(label);
    }
    if (value === null) {
        parts.push(element("div", "spinner"));
    } else {
        const progress = element("progress");
        progress.max = 1;
        progress.value = Number(value);
        parts.push(progress);
    }
    target.replaceChildren(...parts);
}

function renderPicker(node) {
    const target = node.element;
    target.text.textContent = attribute(node, "Text") ?? "";
    const items = attribute(node, "Items") ?? "";
    if (target.items !== items) {
        target.items = items;
        target.select.replaceChildren(...(items === "" ? [] : items.split("\n")).map((item) => {
            const option = element("option");
            option.textContent = item;
            return option;
        }));
    }
    target.select.selectedIndex = Number(attribute(node, "SelectedIndex") ?? 0);
}
