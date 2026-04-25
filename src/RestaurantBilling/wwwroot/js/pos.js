/* ─── POS State ─── */
const posState = {
  categories: [], items: [], cart: [],
  searchTerm: "", selectedCategoryId: null,
  currentBillId: null, currentBillNo: null,
  selectedTableId: null, selectedTableName: null,
  selectedPayMethod: "Cash"
};

/* ─── Init ─── */
async function posInit() {
  await loadCatalog();
  renderPos();
  bindPosEvents();
  bindSettleModal();
}

/* ─── Catalog ─── */
async function loadCatalog() {
  try {
    const data = await getJSON("/billing/catalog?outletId=1");
    posState.categories = (data.categories || []).map(c => ({ id: c.id, name: c.name, color: c.color }));
    posState.items = (data.items || []).map(i => ({
      id: i.id, name: i.name, categoryId: i.categoryId,
      price: i.price, taxPercent: i.taxPercent || 0, foodType: i.foodType || "veg",
      imageUrl: i.imageUrl || ""
    }));
  } catch { toastr?.error("Unable to load item catalog."); }
}

/* ─── Render ─── */
function renderPos() { renderPosCategories(); renderPosItems(); renderPosCart(); }

function renderPosCategories() {
  const host = document.getElementById("posCategoryTabs") || document.getElementById("posCategories");
  if (!host) return;
  host.innerHTML = "";
  const allEl = document.createElement("div");
  allEl.className = "cat-item" + (posState.selectedCategoryId === null ? " active" : "");
  allEl.innerHTML = `<span class="cat-dot" style="background:#94A3B8"></span>All Items`;
  allEl.addEventListener("click", () => { posState.selectedCategoryId = null; renderPosCategories(); renderPosItems(); });
  host.appendChild(allEl);
  posState.categories.forEach(c => {
    const el = document.createElement("div");
    el.className = "cat-item" + (posState.selectedCategoryId === c.id ? " active" : "");
    el.innerHTML = `<span class="cat-dot" style="background:${c.color || '#FF6B35'}"></span>${c.name}`;
    el.addEventListener("click", () => { posState.selectedCategoryId = c.id; renderPosCategories(); renderPosItems(); });
    host.appendChild(el);
  });
}

function renderPosItems() {
  const host = document.getElementById("posItems");
  const countEl = document.getElementById("posItemsCount");
  if (!host) return;
  host.innerHTML = "";
  const filtered = posState.items.filter(i =>
    (!posState.selectedCategoryId || i.categoryId === posState.selectedCategoryId) &&
    (!posState.searchTerm || i.name.toLowerCase().includes(posState.searchTerm))
  );
  if (countEl) countEl.textContent = filtered.length ? `${filtered.length} items` : "";
  if (!filtered.length) {
    host.innerHTML = `<div style="grid-column:1/-1;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:48px 20px;gap:12px;color:var(--text-muted);"><i class="fas fa-search" style="font-size:36px;opacity:0.2;"></i><p style="margin:0;font-size:13px;">No items found</p></div>`;
    return;
  }
  filtered.forEach(i => {
    const tile = document.createElement("div");
    tile.className = "item-tile";
    const badge = i.foodType === "nonveg" ? "nonveg" : i.foodType === "egg" ? "egg" : "veg";
    const media = i.imageUrl
      ? `<img class="item-tile-img" src="${i.imageUrl}" alt="${i.name}" loading="lazy" />`
      : `<div class="item-tile-img item-tile-img-ph"><i class="fas fa-utensils"></i></div>`;
    tile.innerHTML = `
      ${media}
      <div class="item-tile-top"><div class="item-tile-name">${i.name}</div><span class="item-tile-badge ${badge}"></span></div>
      <div class="item-tile-bottom"><div class="item-tile-price">${fmtINR(i.price)}</div><div class="item-tile-kot">KOT</div></div>`;
    tile.addEventListener("click", () => addPosItem(i));
    host.appendChild(tile);
  });
}

/* ─── Cart ─── */
function addPosItem(item) {
  const ex = posState.cart.find(x => x.itemId === item.id);
  if (ex) ex.qty += 1;
  else posState.cart.push({ itemId: item.id, name: item.name, price: item.price, taxPercent: item.taxPercent || 0, qty: 1 });
  renderPosCart();
}

function removePosItem(itemId) { posState.cart = posState.cart.filter(x => x.itemId !== itemId); renderPosCart(); }

function changePosQty(itemId, delta) {
  const line = posState.cart.find(x => x.itemId === itemId);
  if (!line) return;
  line.qty += delta;
  if (line.qty <= 0) posState.cart = posState.cart.filter(x => x.itemId !== itemId);
  renderPosCart();
}

function calcGrand() {
  return posState.cart.reduce((s, x) => s + x.qty * x.price * (1 + (x.taxPercent || 0) / 100), 0);
}

function renderPosCart() {
  const host = document.getElementById("posCartItems");
  const totalEl = document.getElementById("posGrandTotal");
  const subEl = document.getElementById("posSubTotal");
  const taxEl = document.getElementById("posTaxTotal");
  if (!host || !totalEl) return;
  host.innerHTML = "";

  if (!posState.cart.length) {
    host.innerHTML = `<div class="cart-empty"><div class="cart-empty-icon"><i class="fas fa-shopping-basket"></i></div><p>Cart is empty</p><span>Click any item to add</span></div>`;
    if (subEl) subEl.textContent = fmtINR(0);
    if (taxEl) taxEl.textContent = fmtINR(0);
    totalEl.textContent = fmtINR(0);
    return;
  }

  let subtotal = 0, taxTotal = 0;
  posState.cart.forEach(line => {
    const lineSub = line.qty * line.price;
    const lineTax = lineSub * ((line.taxPercent || 0) / 100);
    subtotal += lineSub; taxTotal += lineTax;
    const row = document.createElement("div");
    row.className = "cart-item";
    row.innerHTML = `
      <div class="cart-item-info"><div class="cart-item-name">${line.name}</div><div class="cart-item-sub">${fmtINR(line.price)} each</div></div>
      <div class="cart-qty-ctrl">
        <button class="qty-btn minus" data-id="${line.itemId}" data-delta="-1">&#8722;</button>
        <span class="qty-val">${line.qty}</span>
        <button class="qty-btn" data-id="${line.itemId}" data-delta="1">&#43;</button>
      </div>
      <div class="cart-item-total">${fmtINR(lineSub + lineTax)}</div>
      <span class="cart-remove" data-remove="${line.itemId}"><i class="fas fa-xmark"></i></span>`;
    host.appendChild(row);
  });
  host.querySelectorAll(".qty-btn").forEach(btn =>
    btn.addEventListener("click", e => { e.stopPropagation(); changePosQty(parseInt(btn.dataset.id), parseInt(btn.dataset.delta)); }));
  host.querySelectorAll(".cart-remove").forEach(btn =>
    btn.addEventListener("click", e => { e.stopPropagation(); removePosItem(parseInt(btn.dataset.remove)); }));

  const grand = subtotal + taxTotal;
  if (subEl) subEl.textContent = fmtINR(subtotal);
  if (taxEl) taxEl.textContent = fmtINR(taxTotal);
  totalEl.textContent = fmtINR(grand);
}

function updateCartHeader() {
  const lbl = document.getElementById("cartBillLabel");
  if (!lbl) return;
  const table = posState.selectedTableName ? `${posState.selectedTableName}` : "New Order";
  const bill = posState.currentBillNo ? ` — ${posState.currentBillNo}` : "";
  const held = posState.currentBillId ? ` <span style="background:var(--warning);color:#fff;font-size:10px;padding:1px 6px;border-radius:10px;vertical-align:middle;">HELD</span>` : "";
  lbl.innerHTML = `Dine-In — ${table}${bill}${held}`;
}

function mapCartItems() {
  return posState.cart.map(x => ({
    itemId: x.itemId, itemName: x.name, qty: x.qty, rate: x.price,
    discountAmount: 0, taxPercent: x.taxPercent || 5, isTaxInclusive: false, taxType: "GST"
  }));
}

/* ─── Event Bindings ─── */
function bindPosEvents() {
  const search = document.getElementById("posSearch");
  if (search) { let t; search.addEventListener("input", e => { clearTimeout(t); t = setTimeout(() => { posState.searchTerm = e.target.value.trim().toLowerCase(); renderPosItems(); }, 250); }); }

  document.getElementById("btnHold")?.addEventListener("click", cancelOrderDraft);
  document.getElementById("btnSettle")?.addEventListener("click", openSettleModal);
  document.getElementById("btnKot")?.addEventListener("click", generateKot);
  document.getElementById("btnKotPrint")?.addEventListener("click", generateKotAndPrint);
  document.getElementById("btnTable")?.addEventListener("click", openTablePicker);
  document.getElementById("btnRecall")?.addEventListener("click", openRecallPanel);
  document.getElementById("btnCancelLines")?.addEventListener("click", () => {
    if (!posState.cart.length) return;
    if (!confirm("Clear all line items from current order?")) return;
    posState.cart = [];
    renderPosCart();
  });
  document.getElementById("posQuickBtn")?.addEventListener("click", async () => {
    await loadCatalog();
    renderPosItems();
    toastr?.info("POS refreshed.");
  });
  document.getElementById("btnBackTables")?.addEventListener("click", async () => {
    await posSaveBeforeBackToTables();
    window.location.href = "/fooler";
  });

  // Table picker events
  document.getElementById("tablePanelClose")?.addEventListener("click", closeTablePicker);
  document.getElementById("tablePanelOverlay")?.addEventListener("click", e => { if (e.target.id === "tablePanelOverlay") closeTablePicker(); });

  // Recall panel events
  document.getElementById("recallPanelClose")?.addEventListener("click", closeRecallPanel);
  document.getElementById("recallPanelOverlay")?.addEventListener("click", e => { if (e.target.id === "recallPanelOverlay") closeRecallPanel(); });

  // New order button (clears cart + bill reference)
  document.getElementById("btnNewOrder")?.addEventListener("click", () => {
    posState.cart = []; posState.currentBillId = null; posState.currentBillNo = null;
    posState.selectedTableId = null; posState.selectedTableName = null;
    updateCartHeader(); renderPosCart();
    const btn = document.getElementById("btnTable");
    if (btn) btn.innerHTML = `<i class="fas fa-chair"></i> Table`;
  });
}

/* ─── Table Picker ─── */
async function openTablePicker() {
  const overlay = document.getElementById("tablePanelOverlay");
  if (!overlay) return;
  overlay.classList.add("show");
  const grid = document.getElementById("tableGrid");
  if (!grid) return;
  grid.innerHTML = `<div style="padding:24px;text-align:center;color:var(--text-muted);">Loading tables...</div>`;
  try {
    const tables = await getJSON("/masters/tables-data?outletId=1");
    if (!tables.length) {
      grid.innerHTML = `<div style="padding:24px;text-align:center;color:var(--text-muted);">No tables found. Add tables in Masters → Tables.</div>`;
      return;
    }
    grid.innerHTML = tables.map(t => `
      <div class="table-pick-tile ${t.tableMasterId === posState.selectedTableId ? 'selected' : ''} ${t.isOccupied && t.tableMasterId !== posState.selectedTableId ? 'occupied' : ''}"
           data-id="${t.tableMasterId}" data-name="${t.tableName}">
        <i class="fas fa-chair" style="font-size:22px;margin-bottom:6px;"></i>
        <div style="font-weight:700;font-size:14px;">${t.tableName}</div>
        <div style="font-size:11px;color:var(--text-muted);">${t.capacity} seats</div>
        <div style="font-size:10px;margin-top:3px;font-weight:600;color:${t.isOccupied && t.tableMasterId !== posState.selectedTableId ? 'var(--danger)' : 'var(--success)'};">
          ${t.isOccupied && t.tableMasterId !== posState.selectedTableId ? 'OCCUPIED' : 'FREE'}
        </div>
      </div>`).join("");
    grid.querySelectorAll(".table-pick-tile").forEach(tile => {
      tile.addEventListener("click", () => {
        posState.selectedTableId = parseInt(tile.dataset.id);
        posState.selectedTableName = tile.dataset.name;
        const btn = document.getElementById("btnTable");
        if (btn) btn.innerHTML = `<i class="fas fa-chair"></i> ${tile.dataset.name}`;
        updateCartHeader();
        closeTablePicker();
      });
    });
  } catch { grid.innerHTML = `<div style="padding:24px;text-align:center;color:var(--danger);">Failed to load tables.</div>`; }
}
function closeTablePicker() { document.getElementById("tablePanelOverlay")?.classList.remove("show"); }

/* ─── Hold Bill ─── */
async function holdBill() {
  if (!posState.cart.length) { toastr?.warning("Add items before holding."); return; }
  const btn = document.getElementById("btnHold");
  if (btn) { btn.disabled = true; btn.innerHTML = `<i class="fas fa-spinner fa-spin"></i>`; }
  try {
    const r = await postJSON("/billing/hold", {
      outletId: 1, billType: "DineIn",
      businessDate: new Date().toISOString().slice(0, 10),
      items: mapCartItems(), billLevelDiscount: 0,
      serviceChargeOptIn: false, serviceChargeAmount: 0,
      tableName: posState.selectedTableName || null
    });
    posState.currentBillId = r.billId;
    posState.currentBillNo = r.billNo;
    updateCartHeader();
    toastr?.success(`Bill ${r.billNo} held successfully. Use Recall to view all held bills.`);
    updateRecallBadge();
  } catch { toastr?.error("Failed to hold bill."); }
  finally { if (btn) { btn.disabled = false; btn.innerHTML = `<i class="fas fa-pause"></i> Hold`; } }
}

function cancelOrderDraft() {
  if (!posState.cart.length) return;
  if (!confirm("Cancel current order items?")) return;
  posState.cart = [];
  posState.currentBillId = null;
  posState.currentBillNo = null;
  posState.selectedTableId = null;
  posState.selectedTableName = null;
  updateCartHeader();
  renderPosCart();
  const btn = document.getElementById("btnTable");
  if (btn) btn.innerHTML = `<i class="fas fa-chair"></i> Table`;
  toastr?.info("Order cancelled.");
}

async function posSaveBeforeBackToTables() {
  if (!posState.selectedTableName || !posState.cart.length || posState.currentBillId) return;
  try {
    const r = await postJSON("/billing/hold", {
      outletId: 1, billType: "DineIn",
      businessDate: new Date().toISOString().slice(0, 10),
      items: mapCartItems(), billLevelDiscount: 0,
      serviceChargeOptIn: false, serviceChargeAmount: 0,
      tableName: posState.selectedTableName || null
    });
    posState.currentBillId = r.billId;
    posState.currentBillNo = r.billNo;
    updateCartHeader();
  } catch {
    // best effort: allow navigation even if autosave fails
  }
}

async function updateRecallBadge() {
  try {
    const bills = await getJSON("/billing/held-bills-detail?outletId=1");
    const badge = document.getElementById("recallBadge");
    if (badge) {
      badge.textContent = bills.length;
      badge.style.display = bills.length > 0 ? "inline-flex" : "none";
    }
  } catch {}
}

/* ─── Recall Panel ─── */
async function openRecallPanel() {
  const overlay = document.getElementById("recallPanelOverlay");
  if (!overlay) return;
  overlay.classList.add("show");
  const list = document.getElementById("recallList");
  if (!list) return;
  list.innerHTML = `<div style="padding:24px;text-align:center;color:var(--text-muted);">Loading held bills...</div>`;
  try {
    const bills = await getJSON("/billing/held-bills-detail?outletId=1");
    if (!bills.length) {
      list.innerHTML = `<div style="padding:24px;text-align:center;color:var(--text-muted);"><i class="fas fa-inbox" style="font-size:32px;opacity:0.3;display:block;margin-bottom:8px;"></i>No held bills.</div>`;
      return;
    }
    list.innerHTML = bills.map(b => `
      <div class="recall-bill-row" data-id="${b.billId}" data-no="${b.billNo}" data-table="${b.tableName || ''}">
        <div style="flex:1;">
          <div style="font-weight:700;font-size:14px;color:var(--accent);">${b.billNo}</div>
          <div style="font-size:12px;color:var(--text-muted);">${b.tableName ? b.tableName + ' · ' : ''}${b.billType} · ${b.billTime}</div>
          <div style="font-size:12px;margin-top:2px;">${(b.items || []).map(i => `${i.name} ×${i.qty}`).join(", ")}</div>
        </div>
        <div style="text-align:right;">
          <div style="font-weight:700;font-size:15px;">${fmtINR(b.grandTotal)}</div>
          <button class="btn-accent recall-load-btn" style="font-size:11px;padding:4px 12px;margin-top:6px;" data-bill='${JSON.stringify(b)}'>
            <i class="fas fa-rotate-left"></i> Recall
          </button>
        </div>
      </div>`).join("");
    list.querySelectorAll(".recall-load-btn").forEach(btn => {
      btn.addEventListener("click", () => {
        const b = JSON.parse(btn.dataset.bill);
        recallBill(b);
        closeRecallPanel();
      });
    });
  } catch { list.innerHTML = `<div style="padding:24px;text-align:center;color:var(--danger);">Failed to load held bills.</div>`; }
}
function closeRecallPanel() { document.getElementById("recallPanelOverlay")?.classList.remove("show"); }

function recallBill(b) {
  posState.currentBillId = b.billId;
  posState.currentBillNo = b.billNo;
  posState.selectedTableName = b.tableName || null;
  posState.cart = (b.items || []).map(i => ({
    itemId: i.itemId, name: i.name, price: i.rate,
    taxPercent: i.taxPercent || 5, qty: i.qty
  }));
  const tableBtn = document.getElementById("btnTable");
  if (tableBtn && b.tableName) tableBtn.innerHTML = `<i class="fas fa-chair"></i> ${b.tableName}`;
  updateCartHeader();
  renderPosCart();
  toastr?.info(`Bill ${b.billNo} recalled.`);
}

/* ─── KOT ─── */
async function generateKot() {
  if (!posState.cart.length) { toastr?.warning("Add items before generating KOT."); return; }
  const btn = document.getElementById("btnKot");
  if (btn) { btn.disabled = true; btn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Sending...`; }
  try {
    // Hold first if no bill exists yet
    if (!posState.currentBillId) {
      const held = await postJSON("/billing/hold", {
        outletId: 1, billType: "DineIn",
        businessDate: new Date().toISOString().slice(0, 10),
        items: mapCartItems(), billLevelDiscount: 0,
        serviceChargeOptIn: false, serviceChargeAmount: 0,
        tableName: posState.selectedTableName || null
      });
      posState.currentBillId = held.billId;
      posState.currentBillNo = held.billNo;
      updateCartHeader();
    }
    const result = await postJSON("/kitchen/generate", {
      outletId: 1, billId: posState.currentBillId, captainUserId: 1
    });
    const msg = result.reused
      ? `KOT already exists for this bill.`
      : `KOT sent to kitchen! Bill: ${posState.currentBillNo}`;
    toastr?.success(msg);
    updateRecallBadge();
  } catch { toastr?.error("Failed to generate KOT."); }
  finally { if (btn) { btn.disabled = false; btn.innerHTML = `<i class="fas fa-paper-plane"></i> Send KOT`; } }
}

async function generateKotAndPrint() {
  const printBtn = document.getElementById("btnKotPrint");
  if (printBtn) {
    printBtn.disabled = true;
    printBtn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Printing...`;
  }
  await generateKot();
  if (posState.currentBillNo) window.print();
  if (printBtn) {
    printBtn.disabled = false;
    printBtn.innerHTML = `<i class="fas fa-print"></i> KOT & print`;
  }
}

/* ─── Settle Modal ─── */
function bindSettleModal() {
  document.getElementById("settleClose")?.addEventListener("click", closeSettleModal);
  document.getElementById("settleCancelBtn")?.addEventListener("click", closeSettleModal);
  document.getElementById("settleOverlay")?.addEventListener("click", e => { if (e.target === e.currentTarget) closeSettleModal(); });
  document.getElementById("settleConfirmBtn")?.addEventListener("click", confirmSettle);
  document.querySelectorAll(".settle-pay-btn[data-method]").forEach(btn =>
    btn.addEventListener("click", () => {
      document.querySelectorAll(".settle-pay-btn[data-method]").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      posState.selectedPayMethod = btn.dataset.method;
    }));
  document.getElementById("settleAmtInput")?.addEventListener("input", () => {
    const grand = calcGrand();
    const paid = parseFloat(document.getElementById("settleAmtInput").value) || 0;
    const row = document.getElementById("settleChangeRow");
    const due = document.getElementById("settleChangeDue");
    if (row && due) { row.style.display = paid >= grand ? "block" : "none"; due.textContent = fmtINR(Math.max(0, paid - grand)); }
  });
}

function openSettleModal() {
  if (!posState.cart.length) { toastr?.warning("Cart is empty."); return; }
  const grand = calcGrand();
  const sub = posState.cart.reduce((s, x) => s + x.qty * x.price, 0);
  const tax = grand - sub;
  const summaryEl = document.getElementById("settleSummary");
  if (summaryEl) summaryEl.innerHTML = `
    ${posState.cart.map(l => `<div class="settle-summary-row"><span>${l.name} ×${l.qty}</span><span>${fmtINR(l.qty * l.price)}</span></div>`).join("")}
    <div class="settle-summary-row" style="border-top:1px solid var(--border);margin-top:6px;padding-top:6px;"><span>Subtotal</span><span>${fmtINR(sub)}</span></div>
    <div class="settle-summary-row"><span>Tax</span><span>${fmtINR(tax)}</span></div>
    <div class="settle-summary-row grand"><span>Grand Total</span><span>${fmtINR(grand)}</span></div>`;
  const amtInput = document.getElementById("settleAmtInput");
  if (amtInput) amtInput.value = grand.toFixed(2);
  document.getElementById("settleChangeRow").style.display = "none";
  document.getElementById("settleOverlay")?.classList.add("show");
}

function closeSettleModal() { document.getElementById("settleOverlay")?.classList.remove("show"); }

async function confirmSettle() {
  const method = posState.selectedPayMethod || "Cash";
  const grand = calcGrand();
  const amtPaid = parseFloat(document.getElementById("settleAmtInput")?.value || grand);
  const confirmBtn = document.getElementById("settleConfirmBtn");
  if (confirmBtn) { confirmBtn.disabled = true; confirmBtn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Processing...`; }

  try {
    let result;
    if (posState.currentBillId) {
      // Settle the already-held bill
      result = await postJSON(`/billing/settle-existing/${posState.currentBillId}`, {
        outletId: 1,
        payments: [{ mode: method, amount: grand }]
      });
    } else {
      // Create + settle in one shot (no held bill yet)
      result = await postJSON("/billing/settle", {
        outletId: 1, billType: "DineIn",
        businessDate: new Date().toISOString().slice(0, 10),
        isInterState: false, billLevelDiscount: 0,
        serviceChargeOptIn: false, serviceChargeAmount: 0,
        items: mapCartItems(),
        payments: [{ mode: method, amount: grand }]
      });
    }

    closeSettleModal();
    printReceipt(result, amtPaid, method);

    posState.cart = []; posState.currentBillId = null; posState.currentBillNo = null;
    posState.selectedTableId = null; posState.selectedTableName = null;
    updateCartHeader();
    renderPosCart();
    const tableBtn = document.getElementById("btnTable");
    if (tableBtn) tableBtn.innerHTML = `<i class="fas fa-chair"></i> Table`;
    updateRecallBadge();
    toastr?.success(`Bill ${result.billNo || ""} settled!`);
  } catch (_e) {
    toastr?.error("Settlement failed. Please try again.");
  } finally {
    if (confirmBtn) { confirmBtn.disabled = false; confirmBtn.innerHTML = `<i class="fas fa-check-circle"></i> Confirm & Print`; }
  }
}

/* ─── Print ─── */
function printReceipt(bill, amtPaid, method) {
  const grand = bill?.grandTotal ?? calcGrand();
  const change = Math.max(0, amtPaid - grand);
  const items = (bill?.items || posState.cart).map(l =>
    `<tr><td>${l.itemName || l.name}</td><td style="text-align:center">${l.qty}</td><td style="text-align:right">${fmtINR((l.rate || l.price) * l.qty)}</td></tr>`
  ).join("");
  const tableLine = posState.selectedTableName ? `<div style="font-size:11px;color:#666;">Table: ${posState.selectedTableName}</div>` : "";
  const html = `<div style="font-family:'Courier New',monospace;font-size:12px;max-width:300px;margin:0 auto;padding:10px;">
    <div style="text-align:center;margin-bottom:10px;">
      <div style="font-size:18px;font-weight:bold;">RestoBill</div>
      <div style="font-size:11px;color:#666;">Dine-In Receipt</div>
      ${tableLine}
      <div style="font-size:11px;color:#666;">${new Date().toLocaleString("en-IN")}</div>
      <div style="font-size:12px;font-weight:bold;margin-top:4px;">Bill: ${bill?.billNo || posState.currentBillNo || "---"}</div>
    </div>
    <hr style="border-top:1px dashed #000;margin:6px 0;"/>
    <table style="width:100%;border-collapse:collapse;">
      <thead><tr><th style="text-align:left">Item</th><th style="text-align:center">Qty</th><th style="text-align:right">Amt</th></tr></thead>
      <tbody>${items}</tbody>
    </table>
    <hr style="border-top:1px dashed #000;margin:6px 0;"/>
    <div style="display:flex;justify-content:space-between;font-weight:bold;font-size:14px;margin-top:4px;"><span>TOTAL</span><span>${fmtINR(grand)}</span></div>
    <div style="display:flex;justify-content:space-between;margin-top:4px;"><span>Paid (${method})</span><span>${fmtINR(amtPaid)}</span></div>
    ${change > 0 ? `<div style="display:flex;justify-content:space-between;"><span>Change</span><span>${fmtINR(change)}</span></div>` : ""}
    <hr style="border-top:1px dashed #000;margin:8px 0;"/>
    <div style="text-align:center;font-size:11px;color:#666;">Thank you! Visit again.</div>
  </div>`;
  const wrap = document.getElementById("receiptWrap");
  if (wrap) wrap.innerHTML = html;
  window.print();
}

/* ─── Recall from bill detail / query string ─── */
async function posTryRecallFromQuery() {
  const id = new URLSearchParams(window.location.search).get("recall");
  if (!id) return;
  try {
    const b = await getJSON(`/billing/held-bill/${encodeURIComponent(id)}?outletId=1`);
    if (b.billType && b.billType !== "DineIn") {
      toastr?.warning("This order is not Dine-In. Open Takeaway POS to continue.");
      return;
    }
    recallBill(b);
    const u = new URL(window.location.href);
    u.searchParams.delete("recall");
    history.replaceState({}, "", u.pathname + u.search + u.hash);
  } catch {
    toastr?.error("Could not load this bill. It may not be a draft or the link is invalid.");
  }
}

async function posTryRecallFromPath() {
  const m = window.location.pathname.match(/\/(?:billing\/)?pos\/order\/(\d+)/i);
  if (!m || !m[1]) return;
  const id = m[1];
  try {
    const b = await getJSON(`/billing/held-bill/${encodeURIComponent(id)}?outletId=1`);
    if (b.billType && b.billType !== "DineIn") {
      toastr?.warning("This order is not Dine-In. Open Takeaway POS to continue.");
      return;
    }
    recallBill(b);
  } catch {
    toastr?.error("Could not load this order.");
  }
}

function posTryPreselectTableFromQuery() {
  const query = new URLSearchParams(window.location.search);
  const tableName = query.get("table");
  const tableId = query.get("tableId");
  if (!tableName) return;
  posState.selectedTableName = tableName;
  posState.selectedTableId = tableId ? parseInt(tableId, 10) : null;
  const btn = document.getElementById("btnTable");
  if (btn) btn.innerHTML = `<i class="fas fa-chair"></i> ${tableName}`;
  updateCartHeader();
}

/* ─── Boot ─── */
document.addEventListener("DOMContentLoaded", () => {
  if (document.getElementById("posRoot")) {
    void (async () => {
      await posInit();
      posTryPreselectTableFromQuery();
      await updateRecallBadge();
      await posTryRecallFromPath();
      await posTryRecallFromQuery();
    })();
  }
});
