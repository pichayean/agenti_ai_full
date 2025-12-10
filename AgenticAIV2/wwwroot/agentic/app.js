// ===== Config API =====
const API_URL = "/Evaluation"; // หรือใส่เต็ม URL ก็ได้ถ้าอยู่คนละ origin

// ===== DOM Elements =====
const loadBtn = document.getElementById("load-btn");
const progressContainer = document.getElementById("progress-container");
const progressBar = document.getElementById("progress-bar");
const progressLabel = document.getElementById("progress-label");
const summaryEl = document.getElementById("summary");
const tableBody = document.querySelector("#test-table tbody");
const lastUpdatedEl = document.getElementById("last-updated");
const jsonOutput = document.getElementById("jsonOutput");

let data = null;
let progressTimer = null;

// ===== Progress Bar =====
function startProgress() {
    let p = 0;
    if (!progressBar) return;

    progressBar.style.width = "0%";

    progressTimer = setInterval(() => {
        if (p < 90) {
            p += 5;
            progressBar.style.width = p + "%";
        }
    }, 200);
}

function stopProgress() {
    if (progressTimer) {
        clearInterval(progressTimer);
        progressTimer = null;
    }
    if (progressBar) {
        progressBar.style.width = "100%";
    }
}

// แสดง/ซ่อน progress + disable ปุ่ม
function setLoading(isLoading, text = "กำลังโหลด...") {
    if (!progressContainer || !progressLabel || !loadBtn) return;

    if (isLoading) {
        loadBtn.disabled = true;
        progressContainer.style.display = "flex";  // ← โชว์
        progressLabel.textContent = text;
        startProgress();
    } else {
        loadBtn.disabled = false;
        progressContainer.style.display = "none";
        stopProgress();
    }
}

// ===== Call API =====
async function loadDataFromApi() {
    const res = await fetch(API_URL, {
        method: "GET",
        headers: {
            accept: "text/plain"
        }
    });

    if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
    }

    const text = await res.text();
    const trimmed = text.trim();

    // สมมติ API ตอบเป็น JSON string ใน text/plain
    const parsed = JSON.parse(trimmed);
    data = parsed;
    console.log(data);

    if (jsonOutput) {
        jsonOutput.textContent = JSON.stringify(parsed, null, 2);
    }

    return parsed;
}

// ===== Download JSON =====
function downloadJson() {
    if (!data) {
        alert("ยังไม่มีข้อมูลจาก API กรุณากดโหลดข้อมูลก่อน");
        return;
    }

    const blob = new Blob([JSON.stringify(data, null, 2)], {
        type: "application/json"
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "data.json";
    link.click();
}

// export ให้ปุ่ม onclick ใน HTML เรียกได้
window.downloadJson = downloadJson;

// ===== Helper Format / Utils =====
function formatPercent(v) {
    if (v == null) return "-";
    return (v * 100).toFixed(1) + "%";
}

function formatScore(v) {
    if (v == null) return "-";
    return Number(v).toFixed(3);
}

function htmlEscape(str) {
    if (str == null) return "";
    return String(str)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function formatStepOutput(out) {
    if (out == null) return "";
    if (typeof out === "string") return out;
    try {
        return JSON.stringify(out, null, 2);
    } catch {
        return String(out);
    }
}

// ===== Summary Cards =====
function createSummaryCards(list) {
    if (!summaryEl) return;
    if (!Array.isArray(list)) return;

    const total = list.length;
    const passedCount = list.filter(d => d.overallPassed || d.passed).length;

    const avgAccuracy =
        list.reduce((s, d) => s + (d.accuracy || 0), 0) / (total || 1);
    const avgPlanValidity =
        list.reduce((s, d) => s + (d.planValidity || 0), 0) / (total || 1);
    const avgOverall =
        list.reduce((s, d) => s + (d.overallScore || 0), 0) / (total || 1);

    summaryEl.innerHTML = "";

    const cards = [
        {
            title: "จำนวน Test Case",
            value: total,
            extra: ""
        },
        {
            title: "ผ่าน (Overall)",
            value: `${passedCount}/${total}`,
            extra: `ผ่าน ${((passedCount / (total || 1)) * 100).toFixed(1)}%`
        },
        {
            title: "Avg Accuracy",
            value: formatPercent(avgAccuracy),
            extra: "accuracy จาก detail.accuracy.coverage"
        },
        {
            title: "Avg PlanValidity",
            value: formatPercent(avgPlanValidity),
            extra: "planValidity จาก header"
        },
        {
            title: "Avg OverallScore",
            value: formatScore(avgOverall),
            extra: "overallScore จาก header"
        }
    ];

    for (const c of cards) {
        const div = document.createElement("div");
        div.className = "card";
        div.innerHTML = `
      <div class="card-title">${c.title}</div>
      <div class="card-value">${c.value}</div>
      <div class="card-extra">${c.extra}</div>
    `;
        summaryEl.appendChild(div);
    }
}

// ===== Table =====
function createTableRows(list) {
    if (!tableBody) return;
    if (!Array.isArray(list)) return;

    tableBody.innerHTML = "";

    list.forEach((item, idx) => {
        const mainRow = document.createElement("tr");

        const overallPassed = item.overallPassed ?? item.passed;
        const acc = item.detail?.accuracy?.coverage ?? item.accuracy;
        const runId = item.runId || "";
        const shortRunId = runId ? runId.slice(0, 8) + "..." : "-";

        mainRow.innerHTML = `
      <td>${item.testCaseId ?? "-"}</td>
      <td title="${runId}">${shortRunId}</td>
      <td>
        <span class="badge ${overallPassed ? "badge-pass" : "badge-fail"}">
          ${overallPassed ? "PASS" : "FAIL"}
        </span>
      </td>
      <td>${item.wtr ?? "-"}</td>
      <td>${formatPercent(item.planValidity)}</td>
      <td>${formatPercent(acc)}</td>
      <td>${formatScore(item.overallScore)}</td>
      <td>
        <button class="btn-toggle" data-index="${idx}">
          <span class="icon">▼</span>
          <span>Detail</span>
        </button>
      </td>
    `;

        const detailRow = document.createElement("tr");
        detailRow.className = "detail-row";
        detailRow.style.display = "none";

        const evdt = item.evdt || {};
        const detailMeta = item.detail || {};

        const finalSummary = (evdt.summary || item.summary || "").trim();
        const finalText = (evdt.final || "").trim();

        let emailText = "";
        if (evdt.email) {
            let emailData = evdt.email || "";
            let emailJson = null;
            try {
                // ถ้าเป็น string → parse
                if (typeof emailData === "string") {
                    emailJson = JSON.parse(emailData);
                } else {
                    emailJson = emailData;
                }
            } catch (e) {
                console.error("Invalid email json:", e);
                emailJson = null;
            }
            let args = emailJson?.Args;

            console.log(args);
            if (!args) {
                emailText = `<p>ไม่พบข้อมูลอีเมล</p>`;
            } else {
                const toList = Array.isArray(args.to)
                    ? args.to.join(", ")
                    : "(ไม่มีผู้รับ)";
                const subject = args.subject || "(ไม่มีหัวข้อ)";
                const body = args.body_text || args.body || "";

                emailText = `
                📨 To: ${toList}\n
                📧 Subject: ${subject}\n\n
                ${JSON.stringify(body)}
            `;
            }
        }

        const toolsInfo = detailMeta.tools
            ? JSON.stringify(detailMeta.tools, null, 2)
            : "";
        const planValidityInfo = detailMeta.planValidity
            ? JSON.stringify(detailMeta.planValidity, null, 2)
            : "";
        const accuracyInfo = detailMeta.accuracy
            ? JSON.stringify(detailMeta.accuracy, null, 2)
            : "";
        const acctxt = detailMeta.overallScoreTxt;

        // ===== Journal Block (evdt.journal.steps) =====
        const journalSteps = evdt.journal?.steps || [];

        const journalBlockHtml = journalSteps.length
            ? `
      <div class="journal-block">
        <h4>Execution Journal (evdt.journal.steps)</h4>
        <div class="journal-chain">
          ${journalSteps
                .map((s, i) => {
                    const node = `
                    <div class="journal-node" data-step-index="${i}">
                      <div class="journal-node-main">
                        <div class="journal-node-title">${htmlEscape(s.id || "(no id)")}</div>
                        <div class="journal-node-meta">
                          <span class="badge badge-status-${(s.status || "unknown").toLowerCase()}">
                            ${htmlEscape(s.status || "unknown")}
                          </span>
                          <span class="journal-node-duration">
                            ${s.durationMs != null ? `${s.durationMs} ms` : "-"}
                          </span>
                        </div>
                        <button type="button"
                                class="btn-step-toggle"
                                data-step-index="${i}">
                          ดู Input/Output
                        </button>
                      </div>
                      <div class="journal-io" style="display:none;">
                        <div class="journal-io-box">
                          <div class="journal-io-title">Input</div>
                          <pre>${htmlEscape(formatStepOutput(s.input)) || "(no input)"}</pre>
                        </div>
                        <div class="journal-io-box">
                          <div class="journal-io-title">Output</div>
                          <pre>${htmlEscape(formatStepOutput(s.output)) || "(no output)"}</pre>
                        </div>
                      </div>
                    </div>
                  `;
                    const arrow =
                        i < journalSteps.length - 1
                            ? `<div class="journal-arrow">→</div>`
                            : "";
                    return node + arrow;
                })
                .join("")}
        </div>
      </div>
    `
            : `
      <div class="journal-block">
        <h4>Execution Journal (evdt.journal.steps)</h4>
        <p>(no journal steps)</p>
      </div>
    `;

        // ตอนนี้ table มี 8 คอลัมน์ → colspan="8"
        detailRow.innerHTML = `
      <td class="detail-cell" colspan="8">
        <div class="detail-content">
          <div class="detail-grid">
            <div class="detail-block">
              <h4>Summary / Overall</h4>
              <pre>${finalSummary || "(no summary)"}</pre>
              <pre>${acctxt || "(no acctxt)"}</pre>
            </div>
            <div class="detail-block">
              <h4>Final Output (evdt.final)</h4>
              <pre>${finalText || "(no final text)"}</pre>
            </div>
          </div>

          <div style="height: 8px;"></div>

          <div class="detail-grid">
            <div class="detail-block">
              <h4>Email (ถ้ามี)</h4>
              <pre>${emailText || "(no email)"}</pre>
            </div>
            <div class="detail-block">
              <h4>Tools / Metrics Detail</h4>
              <pre>
tools:
${toolsInfo || "(no tools info)"}

planValidity:
${planValidityInfo || "(no planValidity)"}

accuracy:
${accuracyInfo || "(no accuracy detail)"}
              </pre>
            </div>
          </div>

          <div style="height: 12px;"></div>

          ${journalBlockHtml}
        </div>
      </td>
    `;

        tableBody.appendChild(mainRow);
        tableBody.appendChild(detailRow);
    });

    // toggle detail + journal step IO (event delegation)
    tableBody.onclick = (e) => {
        // toggle detail row
        const btnDetail = e.target.closest(".btn-toggle");
        if (btnDetail) {
            const index = Number(btnDetail.dataset.index);
            const rows = Array.from(tableBody.querySelectorAll("tr"));
            const detailRow = rows[index * 2 + 1]; // main + detail

            const isHidden = detailRow.style.display === "none";
            detailRow.style.display = isHidden ? "table-row" : "none";
            const icon = btnDetail.querySelector(".icon");
            if (icon) icon.textContent = isHidden ? "▲" : "▼";
        }

        // toggle journal step IO
        const btnStep = e.target.closest(".btn-step-toggle");
        if (btnStep) {
            const node = btnStep.closest(".journal-node");
            if (!node) return;
            const io = node.querySelector(".journal-io");
            if (!io) return;

            const isHidden =
                io.style.display === "none" || io.style.display === "";
            io.style.display = isHidden ? "flex" : "none";
            btnStep.textContent = isHidden
                ? "ซ่อน Input/Output"
                : "ดู Input/Output";
        }
    };
}

// ===== Last Updated =====
function setLastUpdated() {
    if (!lastUpdatedEl) return;
    const now = new Date();
    lastUpdatedEl.textContent = `อัปเดตล่าสุด: ${now.toLocaleString("th-TH")}`;
}

// ===== Main Button =====
if (loadBtn) {
    loadBtn.addEventListener("click", async () => {
        setLoading(true, "กำลังโหลดจาก API...");

        try {
            const list = await loadDataFromApi();
            createSummaryCards(list);
            createTableRows(list);
            setLastUpdated();
        } catch (err) {
            console.error("โหลดจาก API fail:", err);
            alert(
                "โหลดข้อมูลจาก API ไม่สำเร็จ ดู error ใน console / เช็ก CORS / HTTPS / URL ให้ถูกด้วย"
            );
        } finally {
            setLoading(false);
        }
    });
}

document.addEventListener("DOMContentLoaded", () => {
    const progressContainer = document.getElementById("progress-container");
    if (progressContainer) {
        progressContainer.style.display = "none";
    }
});
