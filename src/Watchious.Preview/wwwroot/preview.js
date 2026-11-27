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
                markParentOfOptions(node, name);
                break;
            }
            case 4: {
                const node = nodes.get(id);
                const name = string();
                node.attributes.delete(name);
                operations?.push(`#${id} remove ${name}`);
                markChanged(node);
                markParentOfOptions(node, name);
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
                // Its place depends on the parent it now has.
                markChanged(child);
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
        placeByOptions(node);
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

// A control's options decide its parent's layout too: a page's slots, a stack's width.
function markParentOfOptions(node, name) {
    if ((name === "HorizontalOptions" || name === "VerticalOptions") && node.parent) {
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
            // The page decides, as on the watches: the checkbox shows IsOn, which a page that refuses the change keeps.
            toggle.input.onchange = () => {
                send(node, "toggled", toggle.input.checked ? "true" : "false");
                toggle.input.checked = attribute(node, "IsOn") === "true";
            };
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
            return element("span", "image");
        case "Apple.Gauge":
            return element("div", "gauge");
        case "Wear.TimeText":
            return element("div", "time-text");
        case "Wear.CurvedText":
            return element("div", "curved-text");
        case "Wear.EdgeButton": {
            const button = element("button", "edge-button");
            button.onclick = () => send(node, "click");
            return button;
        }
        case "DigitalCrownRotation": {
            // The mouse wheel stands in for the crown: down turns the value up, as the crown scrolls a list down.
            const crown = element("div", "vstack crown-rotation");
            crown.tabIndex = 0;
            crown.partialStep = 0;
            crown.onwheel = (event) => {
                event.preventDefault();
                // One notch is one step, whether the browser reports it as about 100 pixels or as 3 lines.
                const notches = event.deltaMode === WheelEvent.DOM_DELTA_PIXEL ? event.deltaY / 100
                    : event.deltaMode === WheelEvent.DOM_DELTA_LINE ? event.deltaY / 3
                    : event.deltaY;
                crown.partialStep += notches;
                const steps = Math.trunc(crown.partialStep);
                crown.partialStep -= steps;
                turnCrown(node, crown, steps);
            };
            return crown;
        }
        default: {
            // A control this page has no element for (https://github.com/Kebechet/Watchious/issues/3): shown as a
            // warning, with its children, instead of dropped.
            console.warn(`Watchious: unknown control ${node.type}`);
            const unknown = element("div", "vstack unknown");
            unknown.dataset.type = node.type;
            return unknown;
        }
    }
}

// The value on By's grid from From, within the range, as the watch hosts step it. Like them, it steps from its own value
// while the wheel turns, since C#'s answers to earlier steps arrive after newer steps, and follows C# again after a
// 0.5 s rest.
function turnCrown(node, crown, steps) {
    const from = Number(attribute(node, "From") ?? 0);
    const through = Math.max(Number(attribute(node, "Through") ?? from), from);
    const by = Number(attribute(node, "By") ?? 1) || 1;
    const isTurning = crown.lastTurn !== undefined && performance.now() - crown.lastTurn < 500;
    const value = isTurning ? crown.value : Number(attribute(node, "Value") ?? from);
    const index = Math.round((value - from) / by) + steps;
    const next = Math.round(Math.min(through, Math.max(from, from + index * by)) * 1e9) / 1e9;
    if (steps !== 0 && next !== value) {
        crown.value = next;
        crown.lastTurn = performance.now();
        send(node, "changed", String(next));
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
                // A control of the other platform, or one on the screen's edges, takes no row.
                if (child.type.startsWith("Wear.") || (platform === "wearos" && child.type.startsWith("Apple."))) {
                    return child.element;
                }
                const row = element("div", "row");
                row.append(child.element);
                return row;
            }));
            return;
        case "Page": {
            // A page that doesn't scroll gives each control with VerticalOptions an equal share of the height the others
            // leave, as the hosts do, and places it in its share.
            const isScrolling = scrolls(node);
            target.replaceChildren(...node.children.map((child) => {
                const vertical = attribute(child, "VerticalOptions");
                if (isScrolling || vertical === null || child.type === "#text") {
                    return child.element;
                }
                const slot = element("div", "slot");
                slot.style.justifyContent = vertical === "Start" ? "flex-start" : vertical === "End" ? "flex-end" : "center";
                slot.append(child.element);
                return slot;
            }));
            target.style.background = pageBackground(attribute(node, "Background"));
            return;
        }
        case "VStack":
        case "HStack": {
            const spacing = Number(attribute(node, "Spacing") ?? defaultSpacing);
            target.style.gap = `${spacing}px`;
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
        case "Image":
            renderImage(node, target);
            return;
        case "Apple.Gauge":
            target.hidden = platform !== "watchos";
            renderGauge(node, target);
            return;
        case "Wear.TimeText": {
            target.hidden = platform !== "wearos";
            const text = attribute(node, "Text");
            const time = new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
            target.textContent = text ? `${text} · ${time}` : time;
            return;
        }
        case "Wear.CurvedText":
            target.hidden = platform !== "wearos";
            renderCurvedText(node.id, attribute(node, "Text") ?? "", target);
            return;
        case "Wear.EdgeButton": {
            target.hidden = platform !== "wearos";
            target.disabled = attribute(node, "IsEnabled") === "false";
            const icon = attribute(node, "Icon");
            if (icon) {
                const glyph = element("span", "icon material-symbols-rounded");
                glyph.textContent = materialSymbols[icon] ?? icon;
                target.replaceChildren(glyph);
                target.title = attribute(node, "Text") ?? "";
            } else {
                target.textContent = attribute(node, "Text") ?? "";
            }
            return;
        }
        default:
            target.replaceChildren(...node.children.map((child) => child.element));
    }
}

// The shared icons (Watchious.Controls.Icon) as Material Symbols, which stand in for the SF Symbols in the watchOS
// frame too.
const materialSymbols = {
    Heart: "favorite", Star: "star", Bell: "notifications", Home: "home", Person: "person", Settings: "settings",
    Info: "info", Warning: "warning", Search: "search", Lock: "lock", Plus: "add", Minus: "remove", Checkmark: "check",
    Close: "close", Edit: "edit", Delete: "delete", Refresh: "refresh", Share: "share", ChevronLeft: "chevron_left",
    ChevronRight: "chevron_right", ChevronUp: "keyboard_arrow_up", ChevronDown: "keyboard_arrow_down",
    Play: "play_arrow", Pause: "pause", Stop: "stop", Microphone: "mic", Speaker: "volume_up", Music: "music_note",
    Phone: "call", Message: "chat", Location: "location_on", Calendar: "calendar_today", Clock: "schedule",
    Timer: "timer", Alarm: "alarm", Run: "directions_run", Walk: "directions_walk", Bike: "directions_bike",
    Swim: "pool", Dumbbell: "fitness_center", Flame: "local_fire_department", Bolt: "bolt", Drop: "water_drop",
    Moon: "bedtime", Sun: "wb_sunny", Battery: "battery_full", Wifi: "wifi", List: "list",
};

// Compose's name of a Material icon as the font's: DirectionsRun as directions_run, Battery5Bar as battery_5_bar, and
// _3dRotation (a Kotlin name can't start with a digit) as 3d_rotation.
function materialSymbolName(composeName) {
    return composeName
        .replace(/([a-z])([A-Z0-9])/g, "$1_$2")
        .replace(/([0-9])([A-Z])/g, "$1_$2")
        .replace(/^_/, "")
        .toLowerCase();
}

// An icon is a glyph of Google's Material Symbols font, which the page loads when it is online (offline, its name
// shows). The platform's own name wins over Icon, as on the watch; an SF Symbol can't be drawn here, so its name shows.
function renderImage(node, target) {
    const width = attribute(node, "Width");
    const height = attribute(node, "Height");
    const description = attribute(node, "Description") ?? "";
    const ownName = attribute(node, platform === "watchos" ? "AppleSymbol" : "MaterialIcon");
    const icon = attribute(node, "Icon");
    if (ownName || icon) {
        const glyph = element("span", "icon");
        if (ownName && platform === "watchos") {
            glyph.classList.add("symbol-name");
            glyph.textContent = ownName;
        } else {
            glyph.classList.add("material-symbols-rounded");
            glyph.textContent = ownName ? materialSymbolName(ownName) : materialSymbols[icon] ?? icon;
            glyph.style.fontSize = height || width ? `${height || width}px` : "";
        }
        glyph.title = description;
        target.replaceChildren(glyph);
        return;
    }

    const image = element("img");
    image.src = `/images/${encodeURIComponent(attribute(node, "Source") ?? "")}`;
    image.alt = description;
    image.style.width = width ? `${width}px` : "";
    image.style.height = height ? `${height}px` : "";
    target.replaceChildren(image);
}

// SwiftUI's gauge styles: a ring with the current value inside, or a bar with the range under it.
function renderGauge(node, target) {
    const from = Number(attribute(node, "From") ?? 0);
    const through = Number(attribute(node, "Through") ?? 1);
    const value = Number(attribute(node, "Value") ?? from);
    const fraction = through > from ? (value - from) / (through - from) : 0;
    const style = attribute(node, "Style") ?? "Automatic";
    const isRing = style === "Circular" || style === "CircularCapacity";
    target.className = isRing ? "gauge ring" : "gauge bar";
    target.style.setProperty("--fraction", fraction);
    const current = element("span", "gauge-value");
    current.textContent = attribute(node, "CurrentValueText") ?? "";
    const label = element("span", "label caption");
    label.textContent = attribute(node, "Text") ?? "";
    if (isRing) {
        target.replaceChildren(current);
        return;
    }
    const track = element("div", "gauge-track");
    const range = element("div", "gauge-range");
    const minimum = element("span");
    minimum.textContent = attribute(node, "MinimumText") ?? "";
    const maximum = element("span");
    maximum.textContent = attribute(node, "MaximumText") ?? "";
    range.append(minimum, current, maximum);
    target.replaceChildren(label, track, range);
}

// Text along the bottom of the round screen, read from left to right. Its path's id is the node's, as a page under the
// shown one keeps its own curved text in the document.
function renderCurvedText(id, text, target) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("viewBox", "0 0 227 227");
    const path = document.createElementNS(svg.namespaceURI, "path");
    path.setAttribute("id", `curved-text-path-${id}`);
    path.setAttribute("d", "M 14 113.5 A 99.5 99.5 0 0 0 213 113.5");
    path.setAttribute("fill", "none");
    const textElement = document.createElementNS(svg.namespaceURI, "text");
    const textPath = document.createElementNS(svg.namespaceURI, "textPath");
    textPath.setAttribute("href", `#curved-text-path-${id}`);
    textPath.setAttribute("startOffset", "50%");
    textPath.setAttribute("text-anchor", "middle");
    textPath.textContent = text;
    textElement.append(textPath);
    svg.append(path, textElement);
    target.replaceChildren(svg);
}

// Blank text is the whitespace between controls in the Razor markup.
function visibleChildren(node) {
    return node.children.filter((child) => child.type !== "#text" || (child.text ?? "").trim() !== "");
}

// A control's HorizontalOptions and VerticalOptions (MAUI's LayoutOptions, see LayoutOptions.cs), with the hosts' rules.
// A stack ignores its children's options along its own direction, and places a child within its own width; a page that
// doesn't scroll puts a control with VerticalOptions in a slot (render's Page). Fill stretches only what stretches by
// itself (a stack, a Button, Toggle or TextField; a ProgressView always spans the width) and centers anything else.
function placeByOptions(node) {
    const style = node.element?.style;
    if (!style || node.type === "#text") {
        return;
    }
    const isInRow = node.parent?.type === "HStack";
    const horizontal = attribute(node, "HorizontalOptions");
    const keepsOwnWidth = keepWidthOptions.includes(horizontal);
    if (isInRow) {
        // Fill centers too: no control stretches vertically.
        style.alignSelf = flexAlignment(attribute(node, "VerticalOptions"), false);
    } else {
        style.alignSelf = flexAlignment(horizontal, stretchingTypes.has(node.type) || stackTypes.has(node.type));
    }
    if (stackTypes.has(node.type)) {
        // A stack spans the width unless it is in a row or keeps its own width; then it is as wide as its widest child,
        // unless a child stretches across it, and in a row it gets the width its siblings leave.
        const hasStretchingChild = node.children.some(stretchesAcrossStack);
        style.width = (isInRow || keepsOwnWidth) && !hasStretchingChild ? "auto" : "";
        style.flex = isInRow && hasStretchingChild ? "1 1 0" : "";
        style.minWidth = isInRow && hasStretchingChild ? "0" : "";
    } else if (stretchingTypes.has(node.type)) {
        style.width = keepsOwnWidth && !isInRow ? "auto" : "";
    }
}

const keepWidthOptions = ["Start", "Center", "End"];
const stretchingTypes = new Set(["Button", "Toggle", "TextField"]);
const stackTypes = new Set(["VStack", "DigitalCrownRotation"]);

function stretchesAcrossStack(child) {
    if (keepWidthOptions.includes(attribute(child, "HorizontalOptions"))) {
        return false;
    }
    return stretchingTypes.has(child.type) || stackTypes.has(child.type)
        || (child.type === "ProgressView" && attribute(child, "Style") === "Linear");
}

// A page's background as C# resolved it (PageBackground.cs): a color as #RRGGBBAA, or "linear x1 y1 x2 y2" or
// "radial cx cy r" followed by each stop's color and offset, with MAUI's page-relative points. Both gradients are drawn
// in the screen's pixels, as the hosts draw them: a linear one as an SVG gradient between the two points (its bands
// square to the line on screen, not in a stretched unit square), a radial one as a CSS circle sized from the longer side.
function pageBackground(value) {
    if (!value) {
        return "";
    }
    const parsed = parseBackground(value.split(" "));
    if (!parsed) {
        console.warn(`Watchious: unknown Background value ${value} on Page`);
        return "";
    }
    if (parsed.shape === "color") {
        return parsed.color;
    }
    const screen = document.querySelector(".screen").getBoundingClientRect();
    if (parsed.shape === "linear") {
        const [x1, y1, x2, y2] = parsed.numbers;
        const svgStops = parsed.stops.map((stop) =>
            `<stop offset='${stop.offset}' stop-color='${stop.color.slice(0, 7)}' stop-opacity='${parseInt(stop.color.slice(7), 16) / 255}'/>`).join("");
        const svg = `<svg xmlns='http://www.w3.org/2000/svg' preserveAspectRatio='none' viewBox='0 0 ${screen.width} ${screen.height}'>`
            + `<linearGradient id='g' gradientUnits='userSpaceOnUse' x1='${x1 * screen.width}' y1='${y1 * screen.height}' `
            + `x2='${x2 * screen.width}' y2='${y2 * screen.height}'>${svgStops}</linearGradient>`
            + `<rect width='${screen.width}' height='${screen.height}' fill='url(#g)'/></svg>`;
        return `center / 100% 100% no-repeat url("data:image/svg+xml,${encodeURIComponent(svg)}")`;
    }
    const [cx, cy, radius] = parsed.numbers;
    const cssStops = parsed.stops.map((stop) => `${stop.color} ${stop.offset * 100}%`).join(", ");
    return `radial-gradient(circle ${Math.max(radius * Math.max(screen.width, screen.height), 0.5)}px at ${cx * 100}% ${cy * 100}%, ${cssStops})`;
}

// Null for a value the hosts would refuse too.
function parseBackground(parts) {
    const isColor = (text) => /^#[0-9A-Fa-f]{8}$/.test(text);
    const toNumber = (text) => (text === "" ? NaN : Number(text));
    if (parts.length === 1) {
        return isColor(parts[0]) ? { shape: "color", color: parts[0] } : null;
    }
    const [shape, ...rest] = parts;
    const count = shape === "linear" ? 4 : shape === "radial" ? 3 : 0;
    const stopParts = rest.slice(count);
    if (count === 0 || rest.length < count || stopParts.length < 4 || stopParts.length % 2 !== 0) {
        return null;
    }
    const numbers = rest.slice(0, count).map(toNumber);
    const stops = [];
    for (let index = 0; index < stopParts.length; index += 2) {
        stops.push({ color: stopParts[index], offset: toNumber(stopParts[index + 1]) });
    }
    const isValid = numbers.every(Number.isFinite)
        && stops.every((stop) => isColor(stop.color) && Number.isFinite(stop.offset));
    return isValid ? { shape, numbers, stops } : null;
}

function scrolls(node) {
    return node.type === "List" || node.type === "ScrollView" || node.children.some(scrolls);
}

function flexAlignment(options, stretches) {
    switch (options) {
        case "Start":
            return "flex-start";
        case "Center":
            return "center";
        case "End":
            return "flex-end";
        case "Fill":
            return stretches ? "stretch" : "center";
        default:
            return "";
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
