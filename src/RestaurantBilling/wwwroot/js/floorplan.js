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
  const statusCapsuleClass = (status) => {
    if (status === "running") return "status-capsule running";
    return "status-capsule";
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

  /** Show order label + print/eye icons only when a held bill exists. */
  const floorCardHeadRight = (bill) => {
    if (!bill) return "";
    return `<i class="fas fa-print" aria-hidden="true"></i><i class="fas fa-eye" aria-hidden="true"></i>`;
  };
  const floorCardOrderNumber = (bill) => {
    if (!bill) return "";
    if (bill.billNo != null && String(bill.billNo).trim() !== "") return `Order ${esc(bill.billNo)}`;
    if (bill.billId != null) return `Order #${esc(String(bill.billId))}`;
    return "Order";
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
                  <span class="${statusCapsuleClass(status)}">${statusText(status)}</span>
                  ${orderCount > 0
                    ? `<strong class="meta-value">${orderCount} items</strong>`
                    : `<span class="status-capsule open-capsule">Open</span>`}
                </div>
                <div class="order-number-strong${bill ? "" : " order-number-empty"}">${bill ? floorCardOrderNumber(bill) : "Order"}</div>
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
