// URL API จาก curl
const API_URL = "https://localhost:51384/Evaluation";

const loadBtn = document.getElementById("load-btn");
const progressContainer = document.getElementById("progress-container");
const progressBar = document.getElementById("progress-bar");
const progressLabel = document.getElementById("progress-label");
let data;
// แสดง/ซ่อน progress bar + disable ปุ่ม
function setLoading(isLoading, text = "กำลังโหลด...") {
    if (isLoading) {
        loadBtn.disabled = true;
        progressContainer.hidden = false;
        progressLabel.textContent = text;
    } else {
        loadBtn.disabled = false;
        progressContainer.hidden = true;
    }
}

async function loadDataFromApi() {
    // ถ้า backend ยังไม่เปิด CORS เดี๋ยว browser จะฟ้อง CORS error ตรงนี้
    const res = await fetch(API_URL, {
        method: "GET",
        headers: {
            accept: "text/plain"
        }
    });

    if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
    }








    // API ตอบเป็น text/plain แต่ข้างในคือ JSON → แปลงเอง
    const text = await res.text();
    const trimmed = text.trim();

    data = JSON.parse(trimmed);

    try {

        // แสดงใน div
        output.textContent = JSON.stringify(data, null, 2);

        output.insertAdjacentElement("afterend", link);


    } catch (e) {

    }

    return data;
}

function downloadJson() {
    console.log(data)
    if (!data) {
        alert("ยังไม่มีข้อมูลจาก API กรุณากดโหลดข้อมูลก่อน");
        return;
    }

    const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "data.json";
    link.click();
}

function formatPercent(v) {
    if (v == null) return "-";
    return (v * 100).toFixed(1) + "%";
}

function formatScore(v) {
    if (v == null) return "-";
    return v.toFixed(3);
}

function createSummaryCards(data) {
    const total = data.length;
    const passedCount = data.filter(d => d.overallPassed || d.passed).length;

    const avgAccuracy =
        data.reduce((s, d) => s + (d.accuracy || 0), 0) / (total || 1);
    const avgPlanValidity =
        data.reduce((s, d) => s + (d.planValidity || 0), 0) / (total || 1);
    const avgOverall =
        data.reduce((s, d) => s + (d.overallScore || 0), 0) / (total || 1);

    const summaryEl = document.getElementById("summary");
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

function createTableRows(data) {
    const tbody = document.querySelector("#test-table tbody");
    tbody.innerHTML = "";

    data.forEach((item, idx) => {
        const mainRow = document.createElement("tr");

        const overallPassed = item.overallPassed ?? item.passed;
        const acc = item.detail?.accuracy?.coverage ?? item.accuracy;
        const channelPassed = item.detail?.channels?.passed ?? null;

        mainRow.innerHTML = `
      <td>${item.testCaseId}</td>
      <td title="${item.runId}">${item.runId.slice(0, 8)}...</td>
      <td>
        <span class="badge ${overallPassed ? "badge-pass" : "badge-fail"}">
          ${overallPassed ? "PASS" : "FAIL"}
        </span>
      </td>
      <td>${item.wtr}</td>
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
        const emailText =
            evdt.email && (evdt.email.subject || evdt.email.body)
                ? `Subject: ${evdt.email.subject || "-"}\n\n${evdt.email.body || ""}`
                : "";
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

        detailRow.innerHTML = `
      <td class="detail-cell" colspan="9">
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
        </div>
      </td>
    `;

        tbody.appendChild(mainRow);
        tbody.appendChild(detailRow);
    });

    // toggle detail (ใช้ event delegation)
    tbody.onclick = (e) => {
        const btn = e.target.closest(".btn-toggle");
        if (!btn) return;
        const index = parseInt(btn.dataset.index, 10);
        const rows = Array.from(tbody.querySelectorAll("tr"));
        const detailRow = rows[index * 2 + 1]; // main+detail

        const isHidden = detailRow.style.display === "none";
        detailRow.style.display = isHidden ? "table-row" : "none";
        btn.querySelector(".icon").textContent = isHidden ? "▲" : "▼";
    };
}

function setLastUpdated() {
    const el = document.getElementById("last-updated");
    const now = new Date();
    el.textContent = `อัปเดตล่าสุด: ${now.toLocaleString("th-TH")}`;
}

// เมื่อกดปุ่ม
loadBtn.addEventListener("click", async () => {
    try {
        setLoading(true, "กำลังโหลดจาก API...");
        const data = await loadDataFromApi();
        createSummaryCards(data);
        createTableRows(data);
        setLastUpdated();
        setLoading(false);
    } catch (err) {
        console.error("โหลดจาก API fail:", err);
        setLoading(false, "โหลดผิดพลาด");
        alert("โหลดข้อมูลจาก API ไม่สำเร็จ ดู error ใน console / เช็ก CORS / HTTPS ด้วย");
    }
});

// ถ้าอยาก auto-load ตอนเปิดหน้าเลย ก็เรียก click ตรงนี้
// loadBtn.click();
