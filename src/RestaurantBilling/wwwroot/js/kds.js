function kdsGetJSON(url) {
  return fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } })
    .then(r => {
      if (!r.ok) throw new Error(`Request failed: ${r.status}`);
      return r.json();
    });
}

function kdsPostJSON(url, data) {
  return fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Requested-With": "XMLHttpRequest",
      "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
    },
    body: JSON.stringify(data)
  }).then(r => {
    if (!r.ok) throw new Error(`Request failed: ${r.status}`);
    return r.json();
  });
}

async function initKds() {
  const clock = document.getElementById("kdsClock");
  if (clock) {
    const tick = () => {
      clock.textContent = new Date().toLocaleTimeString("en-IN", { hour12: true, hour: "2-digit", minute: "2-digit", second: "2-digit" });
    };
    tick();
    setInterval(tick, 1000);
  }

  if (window.signalR && document.body.dataset.kdsStation) {
    const station = document.body.dataset.kdsStation;
    const conn = new signalR.HubConnectionBuilder().withUrl("/hubs/kds").withAutomaticReconnect().build();
    conn.on("KotStatusUpdated", payload => {
      if (!payload || payload.stationId?.toString() !== station.toString()) return;
      if (typeof toastr !== "undefined") toastr.info(`KOT #${payload.kotId} -> ${payload.status}`);
    });
    try {
      await conn.start();
      await conn.invoke("JoinStation", parseInt(station, 10));
    } catch (_e) {
      // Ignore realtime connection errors in UI bootstrap.
    }
  }

  const grid = document.getElementById("kdsGrid");
  if (grid && document.body.dataset.kdsStation) {
    try {
      const station = document.body.dataset.kdsStation;
      const rows = await kdsGetJSON(`/kot/kots-data?outletId=1&stationId=${station}`);
      const renderItems = (items) => {
        if (!Array.isArray(items) || items.length === 0) {
          return `<div class="kot-note">No item lines</div>`;
        }
        return `<div class="kot-items-list">${items.map(i => `
          <div class="kot-line">
            <div class="kot-qty-badge">${parseFloat(i.qty || 0)}</div>
            <div class="kot-item-nm">${i.itemName || "Item"}</div>
          </div>
          ${i.note ? `<div class="kot-note">Note: ${i.note}</div>` : ""}
        `).join("")}</div>`;
      };
      grid.innerHTML = rows.length
        ? rows.map(r => `<div class="kot-card">
            <div class="kot-card-head">
              <div class="kot-no-label">${r.kotNo}</div>
              <div class="kot-timer ok">${r.status}</div>
            </div>
            ${renderItems(r.items)}
            <div class="kot-actions">
              <button class="btn-kds start" data-id="${r.kotHeaderId}" data-status="Preparing">Start</button>
              <button class="btn-kds ready" data-id="${r.kotHeaderId}" data-status="Ready">Ready</button>
              <button class="btn-kds served" data-id="${r.kotHeaderId}" data-status="Served">Served</button>
            </div>
          </div>`).join("")
        : `<div class="kot-card"><div class="kot-card-head"><div class="kot-no-label">No active KOT</div></div></div>`;
      grid.querySelectorAll("button[data-id]").forEach(btn => {
        btn.addEventListener("click", async () => {
          await kdsPostJSON("/kot/status", { outletId: 1, kotId: parseInt(btn.dataset.id, 10), status: btn.dataset.status });
          if (typeof toastr !== "undefined") toastr.success("KOT status updated.");
        });
      });
    } catch (_e) {
      grid.innerHTML = `<div class="kot-card alert-urgent"><div class="kot-card-head"><div class="kot-no-label">Unable to load KOTs</div></div></div>`;
    }
  }
}

document.addEventListener("DOMContentLoaded", initKds);
