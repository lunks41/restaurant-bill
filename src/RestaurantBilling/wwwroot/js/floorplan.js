(function () {
  const root = document.getElementById("floorPlanRoot");
  if (!root) return;

  const state = {
    zone: "all",
    tables: [],
    heldBills: []
  };

  const zoneFromName = (name) => {
    const text = (name || "").toLowerCase();
    if (text.includes("ac")) return "ac";
    if (text.includes("first")) return "first";
    if (text.includes("ground")) return "ground";
    return "all";
  };

  const statusFromTable = (table, bill) => {
    if (bill?.kotStatus === "Ready") return "kot-ready";
    if (bill?.kotStatus === "Served") return "served";
    if (bill?.items?.length) return "running";
    if (table?.isOccupied) return "running";
    return "available";
  };

  const statusText = (status) => {
    if (status === "kot-ready") return "KOT ready";
    if (status === "served") return "Served";
    if (status === "running") return "Running";
    return "Available";
  };

  const statusClass = (status) => {
    if (status === "kot-ready") return "kot-ready";
    if (status === "served") return "served";
    if (status === "running") return "running";
    return "available";
  };

  const esc = (v) => String(v ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");

  const billByTable = (tableName) => {
    const matchName = (tableName || "").trim().toLowerCase();
    return state.heldBills.find((b) => ((b.tableName || "").trim().toLowerCase() === matchName));
  };

  /** Order label + print/eye only when a held bill exists for this table. */
  const floorCardHeadRight = (bill) => {
    if (!bill) return "";
    const orderPart =
      bill.billNo != null && String(bill.billNo).trim() !== ""
        ? `Order ${esc(bill.billNo)}`
        : bill.billId != null
          ? `Order #${esc(String(bill.billId))}`
          : "Order";
    return `<span class="order-no">${orderPart}</span><i class="fas fa-print" aria-hidden="true"></i><i class="fas fa-eye" aria-hidden="true"></i>`;
  };

  function render() {
    const host = document.getElementById("floorplanSections");
    if (!host) return;

    const filtered = state.tables.filter((t) => state.zone === "all" || zoneFromName(t.tableName) === state.zone);
    if (!filtered.length) {
      host.innerHTML = '<div class="floorplan-empty">No tables found for this filter.</div>';
      return;
    }

    const groups = new Map();
    filtered.forEach((table) => {
      const zone = zoneFromName(table.tableName);
      const label = zone === "ac" ? "AC" : zone === "first" ? "First" : zone === "ground" ? "Ground" : "Main";
      if (!groups.has(label)) groups.set(label, []);
      groups.get(label).push(table);
    });

    host.innerHTML = Array.from(groups.entries()).map(([zoneLabel, zoneTables]) => `
      <section class="floorplan-section">
        <h3>${zoneLabel}</h3>
        <div class="floorplan-grid">
          ${zoneTables.map((t) => {
            const bill = billByTable(t.tableName);
            const status = statusFromTable(t, bill);
            const orderCount = bill?.items?.reduce((s, i) => s + (i.qty || 0), 0) || 0;
            const previewItems = (bill?.items || []).slice(0, 4)
              .map((i) => `<li>${esc(i.name)} x${i.qty || 0}</li>`)
              .join("");
            return `
              <article class="floor-card ${statusClass(status)}" data-table-id="${t.tableMasterId}" data-table-name="${t.tableName}">
                <div class="head">
                  <span class="icon"><i class="fas fa-chair"></i></span>
                  <span class="head-right">
                    ${floorCardHeadRight(bill)}
                  </span>
                </div>
                <div class="name">${t.tableName}</div>
                <div class="capacity">${t.capacity} seats</div>
                <div class="meta">
                  <span>${statusText(status)}</span>
                  <strong>${orderCount > 0 ? orderCount + " items" : "Open"}</strong>
                </div>
                <div class="zone">${esc(zoneLabel)}</div>
                ${bill ? `<div class="amount">${fmtINR(bill.grandTotal || 0)}</div>` : ""}
                ${bill?.items?.length ? `
                  <div class="order-preview">
                    <div class="preview-title">Order open</div>
                    <ul>${previewItems}</ul>
                  </div>` : ""}
              </article>
            `;
          }).join("")}
        </div>
      </section>
    `).join("");

    host.querySelectorAll(".floor-card").forEach((card) => {
      card.addEventListener("click", () => {
        const tableName = card.getAttribute("data-table-name");
        const tableId = card.getAttribute("data-table-id");
        const bill = billByTable(tableName);
        if (bill?.billId) {
          window.location.href = `/pos/order/${encodeURIComponent(bill.billId)}`;
          return;
        }
        window.location.href = `/pos?table=${encodeURIComponent(tableName)}&tableId=${encodeURIComponent(tableId)}`;
      });
    });
  }

  async function load() {
    const host = document.getElementById("floorplanSections");
    if (host) host.innerHTML = '<div class="floorplan-empty">Loading floor plan...</div>';

    try {
      const [tables, heldBills] = await Promise.all([
        getJSON("/masters/tables-data?outletId=1"),
        getJSON("/pos/held-bills-detail?outletId=1")
      ]);
      state.tables = Array.isArray(tables) ? tables : [];
      state.heldBills = Array.isArray(heldBills) ? heldBills : [];
      render();
    } catch (_) {
      if (host) host.innerHTML = '<div class="floorplan-empty error">Unable to load floor plan right now.</div>';
    }
  }

  function bind() {
    document.querySelectorAll(".floorplan-tab").forEach((tab) => {
      tab.addEventListener("click", () => {
        document.querySelectorAll(".floorplan-tab").forEach((x) => x.classList.remove("active"));
        tab.classList.add("active");
        state.zone = tab.getAttribute("data-zone") || "all";
        render();
      });
    });

    document.getElementById("floorplanRefresh")?.addEventListener("click", load);
    document.getElementById("floorQuickBtn")?.addEventListener("click", load);
  }

  bind();
  void load();
})();
