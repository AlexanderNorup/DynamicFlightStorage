
function logMessages(messages) {
    for (let singleMsg of messages) {
        logMessage(singleMsg);
    }
}

function logMessage(singleMsg) {
    let logList = document.getElementById("logList");
    if (logList === null) {
        return;
    }
    let severity = singleMsg.logLevel;

    let timePill = document.createElement("span");
    timePill.className = "font-monospace me-2 ps-1 pe-1 border rounded text-white bg-" + getBootstrapColorFromSeverity(severity);
    timePill.textContent = (new Date(singleMsg.timestamp)).toLocaleString();
    var text = document.createElement("span");
    text.innerText = singleMsg.message;

    if (singleMsg.exceptionMessage !== null) {
        text.appendChild(document.createElement("br"));
        var exText = document.createElement("b");
        exText.innerText = "Exception: ";
        text.appendChild(exText);
        text.appendChild(document.createElement("br"));
        var exMessage = document.createElement("pre");
        exMessage.innerText = singleMsg.exceptionMessage;
        text.appendChild(exMessage);
    }


    let logEntry = document.createElement("li");
    logEntry.className = "logEntry"
    logEntry.appendChild(timePill);
    logEntry.appendChild(text);
    if (severity === "debug") {
        logEntry.classList.add("debug");
    }

    logList.appendChild(logEntry);

    logList.scrollTo({
        top: logList.scrollHeight,
        left: 0,
        behavior: "smooth",
    });
}

function getBootstrapColorFromSeverity(severity) {
    switch (severity) {
        default:
        case 1: return "secondary";
        case 2: return "info";
        case 3: return "warning";
        case 4: return "danger";
    }
}