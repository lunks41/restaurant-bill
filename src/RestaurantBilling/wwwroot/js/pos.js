/* ─── POS State ─── */
const posState = {
  categories: [], items: [], cart: [],
  searchTerm: "", selectedCategoryId: null,
  currentBillId: null, currentBillNo: null,
  selectedTableId: null, selectedTableName: null,
  selectedPayMethod: "Cash",
  partialSplitEnabled: false,
  billLevelDiscount: 0,
  manualGrandTotal: null,
  hasPendingKot: false,
  customerName: "",
  customerPhone: ""
};
const DEFAULT_BRAND_NAME = window.__defaultBrandName || "RestoBill";
const POS_AUTOSAVE_TOAST_KEY = "pos.autosave.toast";
let kotPrintInProgress = false;
function posNotify(type, message) {
  const t = window.toastr;
  if (t && typeof t[type] === "function") t[type](message);
}
const fmtQty = (value) => {
  const num = Number(value);
  if (!Number.isFinite(num)) return "0";
  return num.toFixed(3).replace(/\.?0+$/, "");
};

function updateKotHintVisibility() {
  const hint = document.getElementById("cartKotHint");
  if (!hint) return;
  hint.style.display = posState.hasPendingKot && posState.cart.length ? "" : "none";
}

function updateKotActionButtons() {
  const hasCartItems = posState.cart.length > 0;
  const canSendKot = posState.hasPendingKot && hasCartItems;
  const canPrintKot = hasCartItems;
  const btnKot = document.getElementById("btnKot");
  const btnKotPrint = document.getElementById("btnKotPrint");
  const status = document.getElementById("posKotStatus");
  if (btnKot) btnKot.disabled = !canSendKot;
  if (btnKotPrint) btnKotPrint.disabled = !canPrintKot;
  if (status) {
    status.textContent = canSendKot ? "Pending KOT" : "KOT Sent";
    status.classList.toggle("sent", !canSendKot);
  }
}

function markPendingKot() {
  posState.hasPendingKot = posState.cart.length > 0;
  updateKotHintVisibility();
  updateKotActionButtons();
}

function clearManualGrandTotal() {
  posState.manualGrandTotal = null;
}

/* ─── Init ─── */
async function posInit() {
  await loadCatalog();
  renderPos();
  bindPosEvents();
  bindSettleModal();
  updateCartHeader();
}

/* ─── Catalog ─── */
async function loadCatalog() {
  try {
    const data = await getJSON("/pos/catalog?outletId=1");
    posState.categories = (data.categories || []).map(c => ({ id: c.id, name: c.name, color: c.color }));
    posState.items = (data.items || []).map(i => ({
      id: i.id, name: i.name, categoryId: i.categoryId,
      price: i.price, taxPercent: i.taxPercent || 0, foodType: i.foodType || "veg",
      imageUrl: i.imageUrl || ""
    }));
  } catch { posNotify("error", "Unable to load item catalog."); }
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
    el.innerHTML = `<span class="cat-dot" style="background:${c.color || '#18181b'}"></span>${c.name}`;
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
    const kotLabel = i.isDirectSale ? "Direct" : "KOT";
    if (i.imageUrl) {
      tile.classList.add("item-tile-bg");
      tile.style.backgroundImage = `url("${String(i.imageUrl).replace(/"/g, "&quot;")}")`;
      tile.innerHTML = `
      <div class="item-tile-status">
        <span class="item-tile-badge ${badge}" aria-hidden="true"></span>
      </div>
      <div class="item-tile-main">
        <div class="item-tile-content">
          <div class="item-tile-name">${i.name}</div>
          <div class="item-tile-bottom">
            <div class="item-tile-price">${fmtINR(i.price)}</div>
            <div class="item-tile-kot">${kotLabel}</div>
          </div>
        </div>
      </div>`;
    } else {
      tile.innerHTML = `
      <div class="item-tile-status">
        <span class="item-tile-badge ${badge}" aria-hidden="true"></span>
      </div>
      <div class="item-tile-main">
        <div class="item-tile-img item-tile-img-ph"><i class="fas fa-utensils"></i></div>
        <div class="item-tile-content">
          <div class="item-tile-name">${i.name}</div>
          <div class="item-tile-bottom">
            <div class="item-tile-price">${fmtINR(i.price)}</div>
            <div class="item-tile-kot">${kotLabel}</div>
          </div>
        </div>
      </div>`;
    }
    tile.addEventListener("click", () => addPosItem(i));
    host.appendChild(tile);
  });
}

/* ─── Cart ─── */
function addPosItem(item) {
  const ex = posState.cart.find(x => x.itemId === item.id);
  if (ex) ex.qty += 1;
  else posState.cart.push({ itemId: item.id, name: item.name, price: item.price, taxPercent: item.taxPercent || 0, qty: 1 });
  clearManualGrandTotal();
  markPendingKot();
  renderPosCart();
}

function removePosItem(itemId) {
  posState.cart = posState.cart.filter(x => x.itemId !== itemId);
  clearManualGrandTotal();
  markPendingKot();
  renderPosCart();
}

function changePosQty(itemId, delta) {
  const line = posState.cart.find(x => x.itemId === itemId);
  if (!line) return;
  line.qty += delta;
  if (line.qty <= 0) posState.cart = posState.cart.filter(x => x.itemId !== itemId);
  clearManualGrandTotal();
  markPendingKot();
  renderPosCart();
}

function getPosTotals() {
  let subtotal = 0;
  const lines = posState.cart.map(line => {
    const lineSub = line.qty * line.price;
    const lineTaxRate = (line.taxPercent || 0) / 100;
    subtotal += lineSub;
    return { lineSub, lineTaxRate };
  });
  const discount = Math.max(0, Math.min(posState.billLevelDiscount || 0, subtotal));
  let taxTotal = 0;
  lines.forEach(l => {
    // Business rule: bill-level discount does not reduce tax base.
    taxTotal += l.lineSub * l.lineTaxRate;
  });
  const rawGrand = subtotal - discount + taxTotal;
  const autoGrand = Math.round(rawGrand);
  const grand = Number.isInteger(posState.manualGrandTotal) && posState.manualGrandTotal >= 0
    ? posState.manualGrandTotal
    : autoGrand;
  const roundOff = grand - rawGrand;
  return { subtotal, discount, taxTotal, rawGrand, roundOff, grand };
}

function calcGrand() {
  return getPosTotals().grand;
}

function renderPosCart() {
  const host = document.getElementById("posCartItems");
  const totalEl = document.getElementById("posGrandTotal");
  const subEl = document.getElementById("posSubTotal");
  const taxEl = document.getElementById("posTaxTotal");
  const taxLabelEl = document.getElementById("posTaxLabel");
  const roundOffEl = document.getElementById("posRoundOff");
  const discountEl = document.getElementById("posDiscountTotal");
  if (!host || !totalEl) return;
  host.innerHTML = "";

  if (!posState.cart.length) {
    host.innerHTML = `<div class="cart-empty"><div class="cart-empty-icon"><i class="fas fa-shopping-basket"></i></div><p>Cart is empty</p><span>Click any item to add</span></div>`;
    if (subEl) subEl.textContent = fmtINR(0);
    if (taxEl) taxEl.textContent = fmtINR(0);
    if (taxLabelEl) taxLabelEl.textContent = "Tax (5.00%)";
    if (roundOffEl) roundOffEl.textContent = fmtINR(0);
    if (discountEl) discountEl.textContent = fmtINR(0);
    totalEl.textContent = fmtINR(0);
    updateKotHintVisibility();
    updateKotActionButtons();
    return;
  }

  let subtotal = 0;
  posState.cart.forEach(line => {
    const lineSub = line.qty * line.price;
    const lineTax = lineSub * ((line.taxPercent || 0) / 100);
    subtotal += lineSub;
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

  const totals = getPosTotals();
  const taxPercents = [...new Set(posState.cart.map(x => Number(x.taxPercent || 0).toFixed(2)))];
  const taxLabel = taxPercents.length === 1 ? `Tax (${taxPercents[0]}%)` : "Tax";
  if (subEl) subEl.textContent = fmtINR(subtotal);
  if (taxEl) taxEl.textContent = fmtINR(totals.taxTotal);
  if (taxLabelEl) taxLabelEl.textContent = taxLabel;
  if (roundOffEl) roundOffEl.textContent = fmtINR(totals.roundOff);
  if (discountEl) discountEl.textContent = fmtINR(totals.discount);
  totalEl.textContent = fmtINR(totals.grand);
  updateKotHintVisibility();
  updateKotActionButtons();
}

function updateCartHeader() {
  const title = document.getElementById("cartHeaderTitle");
  const meta = document.getElementById("cartOrderMeta");
  const tableBtn = document.getElementById("btnTable");
  if (title) title.textContent = posState.currentBillNo ? `Order #${posState.currentBillNo}` : "Order #";
  if (meta) meta.textContent = "OPEN · dine-in";
  if (tableBtn) {
    const label = posState.selectedTableName ? posState.selectedTableName : "Table Name";
    tableBtn.innerHTML = `<i class="fas fa-chair"></i> ${label}`;
  }
}

function mapCartItems() {
  return posState.cart.map(x => ({
    itemId: x.itemId, itemName: x.name, qty: x.qty, rate: x.price,
    discountAmount: 0, taxPercent: x.taxPercent || 5, isTaxInclusive: false, taxType: "GST"
  }));
}

function hasUnsavedOrderChanges() {
  return posState.cart.length > 0 && (posState.hasPendingKot || !posState.currentBillId);
}

function saveDraftOnUnload() {
  if (!posState.cart.length) return;
  if (posState.currentBillId) return; // Existing draft already persisted.
  if (!posState.selectedTableName) return; // Dine-in draft requires table context.

  const payload = {
    outletId: 1,
    billType: "DineIn",
    businessDate: new Date().toISOString().slice(0, 10),
    items: mapCartItems(),
    billLevelDiscount: posState.billLevelDiscount || 0,
    serviceChargeOptIn: false,
    serviceChargeAmount: 0,
    tableName: posState.selectedTableName || null,
    customerName: posState.customerName || null,
    phone: posState.customerPhone || null
  };

  try {
    return fetch("/pos/hold", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Requested-With": "XMLHttpRequest"
      },
      body: JSON.stringify(payload),
      keepalive: true
    })
      .then((r) => (r.ok ? r.json() : null))
      .then((json) => {
        if (json?.billNo) {
          sessionStorage.setItem(POS_AUTOSAVE_TOAST_KEY, String(json.billNo));
        }
      })
      .catch(() => {});
  } catch {
    // Best-effort only; ignore unload-time network errors.
    return Promise.resolve();
  }
}

/* ─── Event Bindings ─── */
function bindPosEvents() {
  const search = document.getElementById("posSearch");
  if (search) { let t; search.addEventListener("input", e => { clearTimeout(t); t = setTimeout(() => { posState.searchTerm = e.target.value.trim().toLowerCase(); renderPosItems(); }, 250); }); }

  const cancelOrderModalEl = document.getElementById("cancelOrderModal");
  const cancelOrderModal = (cancelOrderModalEl && window.bootstrap) ? new window.bootstrap.Modal(cancelOrderModalEl) : null;
  document.getElementById("btnHold")?.addEventListener("click", () => {
    if (!posState.cart.length) return;
    cancelOrderModal?.show();
  });
  document.getElementById("confirmCancelOrderBtn")?.addEventListener("click", async () => {
    cancelOrderModal?.hide();
    await cancelOrderDraft();
  });
  document.getElementById("btnSettle")?.addEventListener("click", openSettleModal);
  document.getElementById("btnPrint")?.addEventListener("click", posPrintBillPreview);
  document.getElementById("btnKot")?.addEventListener("click", generateKot);
  document.getElementById("btnKotPrint")?.addEventListener("click", generateKotAndPrint);
  document.getElementById("btnTable")?.addEventListener("click", openTablePicker);
  document.getElementById("btnCancelLines")?.addEventListener("click", () => {
    if (!posState.cart.length) return;
    if (!confirm("Clear all line items from current order?")) return;
    posState.cart = [];
    posState.billLevelDiscount = 0;
    clearManualGrandTotal();
    posState.hasPendingKot = false;
    renderPosCart();
  });
  document.getElementById("btnEditDiscount")?.addEventListener("click", () => {
    const subtotal = posState.cart.reduce((s, x) => s + x.qty * x.price, 0);
    if (!subtotal) {
      posNotify("info", "Add items first.");
      return;
    }
    const current = Number(posState.billLevelDiscount || 0).toFixed(2);
    const raw = prompt(`Enter discount amount (max ${fmtINR(subtotal)})`, current);
    if (raw === null) return;
    const next = Number(raw);
    if (!Number.isFinite(next) || next < 0) {
      posNotify("warning", "Invalid discount amount.");
      return;
    }
    posState.billLevelDiscount = Math.min(next, subtotal);
    clearManualGrandTotal();
    markPendingKot();
    renderPosCart();
  });
  document.getElementById("btnEditRoundOff")?.addEventListener("click", () => {
    if (!posState.cart.length) {
      posNotify("info", "Add items first.");
      return;
    }
    const totals = getPosTotals();
    const defaultGrand = Number.isInteger(posState.manualGrandTotal) ? posState.manualGrandTotal : totals.grand;
    const raw = prompt("Enter final payable amount (whole number)", Number(defaultGrand || 0).toFixed(2));
    if (raw === null) return;
    const next = Number(raw);
    if (!Number.isFinite(next) || next < 0) {
      posNotify("warning", "Invalid total amount.");
      return;
    }
    posState.manualGrandTotal = Math.round(next);
    renderPosCart();
  });
  document.getElementById("posQuickBtn")?.addEventListener("click", async () => {
    await loadCatalog();
    renderPosItems();
    posNotify("info", "POS refreshed.");
  });
  document.getElementById("btnBackTables")?.addEventListener("click", async () => {
    await posSaveBeforeBackToTables();
    window.location.href = "/floor";
  });
  const customerToggle = document.getElementById("btnCustomerToggle");
  const customerCollapse = document.getElementById("customerCollapse");
  customerToggle?.addEventListener("click", () => {
    const isExpanded = customerToggle.getAttribute("aria-expanded") === "true";
    customerToggle.setAttribute("aria-expanded", String(!isExpanded));
    if (customerCollapse) customerCollapse.hidden = isExpanded;
  });
  document.getElementById("btnSaveCustomer")?.addEventListener("click", () => {
    posState.customerName = (document.getElementById("customerName")?.value || "").trim();
    posState.customerPhone = (document.getElementById("customerPhone")?.value || "").trim();
    posNotify("success", "Customer info saved.");
  });

  // Table picker events
  document.getElementById("tablePanelClose")?.addEventListener("click", closeTablePicker);
  document.getElementById("tablePanelOverlay")?.addEventListener("click", e => { if (e.target.id === "tablePanelOverlay") closeTablePicker(); });

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
        updateCartHeader();
        closeTablePicker();
      });
    });
  } catch { grid.innerHTML = `<div style="padding:24px;text-align:center;color:var(--danger);">Failed to load tables.</div>`; }
}
function closeTablePicker() { document.getElementById("tablePanelOverlay")?.classList.remove("show"); }

/* ─── Hold Bill ─── */
async function holdBill() {
  if (!posState.cart.length) { posNotify("warning", "Add items before holding."); return; }
  const btn = document.getElementById("btnHold");
  if (btn) { btn.disabled = true; btn.innerHTML = `<i class="fas fa-spinner fa-spin"></i>`; }
  try {
    const r = await postJSON("/pos/hold", {
      outletId: 1, billType: "DineIn",
      businessDate: new Date().toISOString().slice(0, 10),
      items: mapCartItems(), billLevelDiscount: posState.billLevelDiscount || 0,
      serviceChargeOptIn: false, serviceChargeAmount: 0,
      tableName: posState.selectedTableName || null,
      customerName: posState.customerName || null,
      phone: posState.customerPhone || null
    });
    posState.currentBillId = r.billId;
    posState.currentBillNo = r.billNo;
    updateCartHeader();
    posNotify("success", `Bill ${r.billNo} held successfully. Use Recall to view all held bills.`);
  } catch { posNotify("error", "Failed to hold bill."); }
  finally { if (btn) { btn.disabled = false; btn.innerHTML = `<i class="fas fa-pause"></i> Hold`; } }
}

async function cancelOrderDraft() {
  if (!posState.cart.length) return;
  const existingBillId = posState.currentBillId;
  if (existingBillId) {
    try {
      await postJSON(`/pos/cancel-draft/${existingBillId}`, { outletId: 1 });
    } catch {
      posNotify("error", "Unable to cancel order right now.");
      return;
    }
  }

  posState.cart = [];
  posState.currentBillId = null;
  posState.currentBillNo = null;
  posState.selectedTableId = null;
  posState.selectedTableName = null;
  posState.billLevelDiscount = 0;
  clearManualGrandTotal();
  posState.hasPendingKot = false;
  updateCartHeader();
  renderPosCart();
  updateCartHeader();
  posNotify("success", "Order cancelled. Table is now free.");
}

async function posSaveBeforeBackToTables() {
  if (!posState.selectedTableName || !posState.cart.length || posState.currentBillId) return;
  try {
    const r = await postJSON("/pos/hold", {
      outletId: 1, billType: "DineIn",
      businessDate: new Date().toISOString().slice(0, 10),
      items: mapCartItems(), billLevelDiscount: posState.billLevelDiscount || 0,
      serviceChargeOptIn: false, serviceChargeAmount: 0,
      tableName: posState.selectedTableName || null,
      customerName: posState.customerName || null,
      phone: posState.customerPhone || null
    });
    posState.currentBillId = r.billId;
    posState.currentBillNo = r.billNo;
    updateCartHeader();
  } catch {
    // best effort: allow navigation even if autosave fails
  }
}

function recallBill(b) {
  posState.currentBillId = b.billId;
  posState.currentBillNo = b.billNo;
  posState.selectedTableName = b.tableName || null;
  posState.billLevelDiscount = Number(b.discountAmount || 0);
  clearManualGrandTotal();
  if (typeof b.hasPendingKot === "boolean") posState.hasPendingKot = b.hasPendingKot;
  else if (typeof b.hasAnyKot === "boolean") posState.hasPendingKot = !b.hasAnyKot;
  else posState.hasPendingKot = true;
  posState.customerName = b.customerName || "";
  posState.customerPhone = b.phone || "";
  posState.cart = (b.items || []).map(i => ({
    itemId: i.itemId, name: i.name, price: i.rate,
    taxPercent: i.taxPercent || 5, qty: i.qty
  }));
  const customerNameEl = document.getElementById("customerName");
  const customerPhoneEl = document.getElementById("customerPhone");
  if (customerNameEl) customerNameEl.value = posState.customerName;
  if (customerPhoneEl) customerPhoneEl.value = posState.customerPhone;
  updateCartHeader();
  renderPosCart();
  posNotify("info", `Bill ${b.billNo} recalled.`);
}

/* ─── KOT ─── */
async function generateKot() {
  if (!posState.cart.length) { posNotify("warning", "Add items before generating KOT."); return; }
  const btn = document.getElementById("btnKot");
  if (btn) { btn.disabled = true; btn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Sending...`; }
  try {
    // Hold first if no bill exists yet; otherwise sync current draft changes before KOT.
    if (!posState.currentBillId) {
      const held = await postJSON("/pos/hold", {
        outletId: 1, billType: "DineIn",
        businessDate: new Date().toISOString().slice(0, 10),
        items: mapCartItems(), billLevelDiscount: posState.billLevelDiscount || 0,
        serviceChargeOptIn: false, serviceChargeAmount: 0,
        tableName: posState.selectedTableName || null,
        customerName: posState.customerName || null,
        phone: posState.customerPhone || null
      });
      posState.currentBillId = held.billId;
      posState.currentBillNo = held.billNo;
      updateCartHeader();
    } else {
      await postJSON(`/pos/update-draft/${posState.currentBillId}`, {
        outletId: 1,
        items: mapCartItems(),
        billLevelDiscount: posState.billLevelDiscount || 0,
        serviceChargeOptIn: false,
        serviceChargeAmount: 0,
        tableName: posState.selectedTableName || null,
        customerName: posState.customerName || null,
        phone: posState.customerPhone || null
      });
    }
    const result = await postJSON("/kot/generate", {
      outletId: 1, billId: posState.currentBillId, captainUserId: 1
    });
    const msg = result.reused
      ? `KOT updated with new items.`
      : `KOT sent to kitchen! Bill: ${posState.currentBillNo}`;
    posState.hasPendingKot = false;
    updateKotHintVisibility();
    updateKotActionButtons();
    posNotify("success", msg);
    return { ok: true, kotIds: Array.isArray(result.kotIds) ? result.kotIds : [] };
  } catch {
    posNotify("error", "Failed to generate KOT.");
    return { ok: false, kotIds: [] };
  }
  finally {
    if (btn) btn.innerHTML = `<i class="fas fa-paper-plane"></i> Send KOT`;
    updateKotActionButtons();
  }
}

async function generateKotAndPrint() {
  const printBtn = document.getElementById("btnKotPrint");
  if (kotPrintInProgress) return;
  if (!posState.cart.length) {
    posNotify("warning", "Add items before printing.");
    return;
  }
  kotPrintInProgress = true;
  if (printBtn) {
    printBtn.disabled = true;
    printBtn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Printing...`;
  }
  try {
    await printKotSlip();
  } finally {
    if (printBtn) {
      printBtn.innerHTML = `<i class="fas fa-print"></i> KOT Print`;
      printBtn.disabled = false;
    }
    kotPrintInProgress = false;
  }
}

/* ─── Settle Modal ─── */
function bindSettleModal() {
  const partialToggle = document.getElementById("settlePartialSplit");
  const partialRow = partialToggle?.closest(".settle-split-row");
  const partialCashGroup = document.getElementById("settlePartialCashGroup");
  const partialCashInput = document.getElementById("settlePartialCashInput");
  const partialSelectedInput = document.getElementById("settlePartialSelectedInput");
  const partialSelectedLabel = document.getElementById("settlePartialSelectedLabel");
  const partialRemaining = document.getElementById("settlePartialRemaining");
  const amountInput = document.getElementById("settleAmtInput");
  const amountEditableToggle = document.getElementById("settleAmtEditable");

  const updatePartialPaymentUi = () => {
    const grand = calcGrand();
    const isPartial = Boolean(partialToggle?.checked) && (posState.selectedPayMethod || "Cash") !== "Cash";
    if (partialCashGroup) partialCashGroup.style.display = isPartial ? "" : "none";
    if (!isPartial) {
      posState.partialSplitEnabled = false;
      if (partialCashInput) partialCashInput.value = "";
      if (partialSelectedInput) partialSelectedInput.value = "";
      if (partialRemaining) partialRemaining.textContent = `Remaining in selected mode: ${fmtINR(grand)}`;
      return;
    }
    posState.partialSplitEnabled = true;
    const selectedMethod = (posState.selectedPayMethod || "Card");
    if (partialSelectedLabel) partialSelectedLabel.textContent = `${selectedMethod} Amount`;
    const cash = Math.max(0, Number.parseFloat(partialCashInput?.value || "0") || 0);
    const clampedCash = Math.min(cash, grand);
    if (partialCashInput && cash !== clampedCash) partialCashInput.value = clampedCash.toFixed(2);
    const remaining = Math.max(0, grand - clampedCash);
    if (partialSelectedInput) partialSelectedInput.value = remaining.toFixed(2);
    if (partialRemaining) partialRemaining.textContent = `Remaining in selected mode: ${fmtINR(remaining)}`;
  };

  const updatePartialSplitState = () => {
    const isCashOnly = (posState.selectedPayMethod || "Cash") === "Cash";
    if (!partialToggle) return;
    partialToggle.disabled = isCashOnly;
    if (partialRow) partialRow.classList.toggle("disabled", isCashOnly);
    if (isCashOnly) {
      partialToggle.checked = false;
      posState.partialSplitEnabled = false;
    }
    updatePartialPaymentUi();
  };

  document.getElementById("settleClose")?.addEventListener("click", closeSettleModal);
  document.getElementById("settleCancelBtn")?.addEventListener("click", closeSettleModal);
  document.getElementById("settleOverlay")?.addEventListener("click", e => { if (e.target === e.currentTarget) closeSettleModal(); });
  document.getElementById("settleConfirmBtn")?.addEventListener("click", confirmSettle);
  document.querySelectorAll(".settle-pay-btn[data-method]").forEach(btn =>
    btn.addEventListener("click", () => {
      document.querySelectorAll(".settle-pay-btn[data-method]").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      posState.selectedPayMethod = btn.dataset.method;
      updatePartialSplitState();
    }));
  partialToggle?.addEventListener("change", () => {
    const checked = Boolean(partialToggle.checked);
    if (checked && (posState.selectedPayMethod || "Cash") === "Cash") {
      partialToggle.checked = false;
      posState.partialSplitEnabled = false;
      posNotify("info", "Choose Card or UPI to enable partial split with cash.");
      return;
    }
    posState.partialSplitEnabled = checked;
    updatePartialPaymentUi();
  });
  partialCashInput?.addEventListener("input", updatePartialPaymentUi);
  amountEditableToggle?.addEventListener("change", () => {
    if (!amountInput) return;
    amountInput.readOnly = !amountEditableToggle.checked;
  });
  amountInput?.addEventListener("input", () => {
    const grand = calcGrand();
    const paid = parseFloat(amountInput.value) || 0;
    const row = document.getElementById("settleChangeRow");
    const due = document.getElementById("settleChangeDue");
    if (row && due) { row.style.display = paid >= grand ? "block" : "none"; due.textContent = fmtINR(Math.max(0, paid - grand)); }
  });
  updatePartialSplitState();
}

function openSettleModal() {
  if (!posState.cart.length) { posNotify("warning", "Cart is empty."); return; }
  const totals = getPosTotals();
  const grand = totals.grand;
  const sub = totals.subtotal;
  const tax = totals.taxTotal;
  const roundOff = totals.roundOff;
  const discount = totals.discount;
  const summaryEl = document.getElementById("settleSummary");
  const billDetailsHtml = `
    <div class="settle-bill-details">
      <div class="settle-bill-detail"><span>Bill No</span><strong>${posState.currentBillNo || "-"}</strong></div>
      <div class="settle-bill-detail"><span>Table</span><strong>${posState.selectedTableName || "-"}</strong></div>
      <div class="settle-bill-detail"><span>Customer</span><strong>${posState.customerName || "-"}</strong></div>
      <div class="settle-bill-detail"><span>Phone</span><strong>${posState.customerPhone || "-"}</strong></div>
    </div>
  `;
  const itemRowsHtml = posState.cart.map(l => `
    <div class="settle-summary-row settle-item-row">
      <span class="settle-item-name">${l.name}</span>
      <span class="settle-item-qty">${fmtQty(l.qty)}</span>
      <span class="settle-item-amount">${fmtINR(l.qty * l.price)}</span>
    </div>
  `).join("");
  if (summaryEl) summaryEl.innerHTML = `
    ${billDetailsHtml}
    <div class="settle-summary-row settle-summary-item-head settle-item-row">
      <span class="settle-item-name">Item</span>
      <span class="settle-item-qty">Qty</span>
      <span class="settle-item-amount">Amount</span>
    </div>
    <div class="settle-summary-items">${itemRowsHtml}</div>
    <div class="settle-summary-row" style="border-top:1px solid var(--border);margin-top:6px;padding-top:6px;"><span>Subtotal</span><span>${fmtINR(sub)}</span></div>
    <div class="settle-summary-row"><span>Tax</span><span>${fmtINR(tax)}</span></div>
    <div class="settle-summary-row"><span>Discount</span><span>${fmtINR(discount)}</span></div>
    <div class="settle-summary-row"><span>RoundOff</span><span>${fmtINR(roundOff)}</span></div>
    <div class="settle-summary-row grand"><span>Grand Total</span><span>${fmtINR(grand)}</span></div>`;
  const amtInput = document.getElementById("settleAmtInput");
  if (amtInput) amtInput.value = Number(grand || 0).toFixed(2);
  const partialToggle = document.getElementById("settlePartialSplit");
  const partialCashInput = document.getElementById("settlePartialCashInput");
  const partialSelectedInput = document.getElementById("settlePartialSelectedInput");
  const partialSelectedLabel = document.getElementById("settlePartialSelectedLabel");
  const partialCashGroup = document.getElementById("settlePartialCashGroup");
  const partialRemaining = document.getElementById("settlePartialRemaining");
  const amountEditableToggle = document.getElementById("settleAmtEditable");
  if (partialToggle) {
    partialToggle.checked = false;
    partialToggle.disabled = (posState.selectedPayMethod || "Cash") === "Cash";
    partialToggle.closest(".settle-split-row")?.classList.toggle("disabled", partialToggle.disabled);
  }
  if (partialCashGroup) partialCashGroup.style.display = "none";
  if (partialCashInput) partialCashInput.value = "";
  if (partialSelectedInput) partialSelectedInput.value = "";
  if (partialSelectedLabel) partialSelectedLabel.textContent = `${posState.selectedPayMethod || "Card"} Amount`;
  if (partialRemaining) partialRemaining.textContent = `Remaining in selected mode: ${fmtINR(grand)}`;
  posState.partialSplitEnabled = false;
  if (amountEditableToggle) amountEditableToggle.checked = false;
  if (amtInput) amtInput.readOnly = true;
  document.getElementById("settleChangeRow").style.display = "none";
  document.getElementById("settleOverlay")?.classList.add("show");
}

function closeSettleModal() { document.getElementById("settleOverlay")?.classList.remove("show"); }

async function posPrintBillPreview() {
  if (!posState.cart.length) {
    posNotify("warning", "Cart is empty.");
    return;
  }
  await printReceipt(
    null,
    calcGrand(),
    posState.selectedPayMethod || "Cash",
    []
  );
}

async function confirmSettle() {
  const method = posState.selectedPayMethod || "Cash";
  const grand = calcGrand();
  const partialToggle = document.getElementById("settlePartialSplit");
  const isPartialSplit = Boolean(partialToggle?.checked) && method !== "Cash";
  const partialCashInput = document.getElementById("settlePartialCashInput");
  const buildPayments = () => {
    if (!isPartialSplit) {
      return [{ mode: method, amount: grand }];
    }
    const enteredCash = Math.max(0, Number.parseFloat(partialCashInput?.value || "0") || 0);
    const cashAmount = Math.min(Math.round(enteredCash), grand);
    const digitalAmount = grand - cashAmount;
    return [
      { mode: "Cash", amount: cashAmount },
      { mode: method, amount: digitalAmount }
    ];
  };
  const payments = buildPayments();
  const totalPaid = payments.reduce((sum, p) => sum + Number(p.amount || 0), 0);
  const typedAmount = parseFloat(document.getElementById("settleAmtInput")?.value || `${totalPaid}`);
  const amtPaid = Number.isFinite(typedAmount) ? typedAmount : totalPaid;
  const confirmBtn = document.getElementById("settleConfirmBtn");
  if (confirmBtn) { confirmBtn.disabled = true; confirmBtn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Processing...`; }

  try {
    let result;
    if (posState.currentBillId) {
      // Settle the already-held bill
      result = await postJSON(`/pos/settle-existing/${posState.currentBillId}`, {
        outletId: 1,
        customerName: posState.customerName || null,
        phone: posState.customerPhone || null,
        payments
      });
    } else {
      // Create + settle in one shot (no held bill yet)
      result = await postJSON("/pos/settle", {
        outletId: 1, billType: "DineIn",
        businessDate: new Date().toISOString().slice(0, 10),
        isInterState: false, billLevelDiscount: posState.billLevelDiscount || 0,
        serviceChargeOptIn: false, serviceChargeAmount: 0,
        tableName: posState.selectedTableName || null,
        customerName: posState.customerName || null,
        phone: posState.customerPhone || null,
        items: mapCartItems(),
        payments
      });
    }

    closeSettleModal();
    await printReceipt(result, amtPaid, method, payments);

    posState.cart = []; posState.currentBillId = null; posState.currentBillNo = null; posState.billLevelDiscount = 0; posState.hasPendingKot = false;
    clearManualGrandTotal();
    posState.selectedTableId = null; posState.selectedTableName = null;
    updateCartHeader();
    renderPosCart();
    updateCartHeader();
    posNotify("success", `Bill ${result.billNo || ""} settled!`);
  } catch (_e) {
    posNotify("error", "Settlement failed. Please try again.");
  } finally {
    if (confirmBtn) { confirmBtn.disabled = false; confirmBtn.innerHTML = `<i class="fas fa-check-circle"></i> Confirm & Print`; }
  }
}

/* ─── Print ─── */
async function printReceipt(bill, amtPaid, method, payments) {
  const liveTotals = getPosTotals();
  const subtotal = bill?.subTotal ?? liveTotals.subtotal;
  const discount = bill?.discountAmount ?? liveTotals.discount;
  const taxTotal = bill?.taxAmount ?? liveTotals.taxTotal;
  const grand = bill?.grandTotal ?? liveTotals.grand;
  const roundOff = bill?.roundOff ?? liveTotals.roundOff;
  const change = Math.max(0, amtPaid - grand);
  const effectivePayments = Array.isArray(payments) && payments.length ? payments : [{ mode: method, amount: amtPaid }];
  const paidTotal = effectivePayments.reduce((sum, p) => sum + (p.amount || 0), 0);
  const items = (bill?.items || posState.cart).map(l =>
    `<tr><td>${l.itemName || l.name}</td><td style="text-align:center">${fmtQty(l.qty)}</td><td style="text-align:right">${fmtINR((l.rate || l.price) * l.qty)}</td></tr>`
  ).join("");
  const paymentRows = effectivePayments.map(p =>
    `<div style="display:flex;justify-content:space-between;margin-top:4px;"><span>Paid (${p.mode})</span><span>${fmtINR(p.amount || 0)}</span></div>`
  ).join("");
  const tableLine = posState.selectedTableName ? `<div style="font-size:11px;color:#666;">Table: ${posState.selectedTableName}</div>` : "";
  const branding = typeof window.getBrandingSettings === "function"
    ? await window.getBrandingSettings()
    : { restaurantName: DEFAULT_BRAND_NAME, logoUrl: "" };
  const brandName = (branding?.restaurantName || DEFAULT_BRAND_NAME);
  const safeLogoUrl = String(branding?.logoUrl || "").trim();
  const logoBlock = safeLogoUrl && !safeLogoUrl.includes("${")
    ? `<img src="${safeLogoUrl}" alt="Logo" style="max-height:48px;max-width:120px;object-fit:contain;margin:0 auto 4px;display:block;" />`
    : "";
  const html = `<div style="font-family:'Courier New',monospace;font-size:12px;max-width:300px;margin:0 auto;padding:10px;">
    <div style="text-align:center;margin-bottom:10px;">
      ${logoBlock}
      <div style="font-size:18px;font-weight:bold;">${brandName}</div>
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
    <div style="display:flex;justify-content:space-between;"><span>Subtotal</span><span>${fmtINR(subtotal)}</span></div>
    <div style="display:flex;justify-content:space-between;"><span>Tax</span><span>${fmtINR(taxTotal)}</span></div>
    <div style="display:flex;justify-content:space-between;"><span>Discount</span><span>${fmtINR(discount)}</span></div>
    <div style="display:flex;justify-content:space-between;"><span>RoundOff</span><span>${fmtINR(roundOff)}</span></div>
    <div style="display:flex;justify-content:space-between;font-weight:bold;font-size:14px;margin-top:4px;"><span>TOTAL</span><span>${fmtINR(grand)}</span></div>
    <hr style="border-top:1px dashed #000;margin:6px 0;"/>
    ${paymentRows}
    <div style="display:flex;justify-content:space-between;margin-top:4px;font-weight:bold;"><span>Total Paid Amount</span><span>${fmtINR(paidTotal)}</span></div>
    ${change > 0 ? `<div style="display:flex;justify-content:space-between;"><span>Change</span><span>${fmtINR(change)}</span></div>` : ""}
    <hr style="border-top:1px dashed #000;margin:8px 0;"/>
    <div style="text-align:center;font-size:11px;color:#666;">Thank you! Visit again.</div>
  </div>`;
  const wrap = document.getElementById("receiptWrap");
  if (wrap) wrap.innerHTML = html;
  posPrintNow();
}

async function printKotSlip() {
  const wrap = document.getElementById("receiptWrap");
  if (!wrap) {
    posPrintNow();
    return;
  }

  let printItems = [];
  if (posState.currentBillId) {
    try {
      const kots = await getJSON("/kot/kots-data?outletId=1");
      const latestKotForBill = (Array.isArray(kots) ? kots : [])
        .filter(k => Number(k.billId) === Number(posState.currentBillId))
        .sort((a, b) => new Date(b.kotDateIso).getTime() - new Date(a.kotDateIso).getTime())[0];
      if (latestKotForBill && Array.isArray(latestKotForBill.items)) {
        printItems = latestKotForBill.items.map(i => ({
          name: i.itemName,
          qty: i.qty,
          status: i.status || latestKotForBill.status || "Pending"
        }));
      }
    } catch {
      // Fallback to local cart data when KOT lookup fails.
    }
  }

  if (!printItems.length) {
    printItems = posState.cart.map(l => ({
      name: l.name,
      qty: l.qty,
      status: l.status || "Pending"
    }));
  }

  const tableLine = posState.selectedTableName ? `<div style="font-size:11px;color:#666;">Table: ${posState.selectedTableName}</div>` : "";
  const branding = typeof window.getBrandingSettings === "function"
    ? await window.getBrandingSettings()
    : { restaurantName: DEFAULT_BRAND_NAME, logoUrl: "" };
  const brandName = (branding?.restaurantName || DEFAULT_BRAND_NAME);
  const safeLogoUrl = String(branding?.logoUrl || "").trim();
  const logoBlock = safeLogoUrl && !safeLogoUrl.includes("${")
    ? `<img src="${safeLogoUrl}" alt="Logo" style="max-height:48px;max-width:120px;object-fit:contain;margin:0 auto 4px;display:block;" />`
    : "";
  const lines = printItems.map((l) => `
    <tr>
      <td>${l.name}</td>
      <td style="text-align:center">${fmtQty(l.qty)}</td>
      <td style="text-align:right"><span style="padding:1px 6px;font-size:10px;">${l.status || "Pending"}</span></td>
    </tr>`).join("");
  wrap.innerHTML = `<div style="font-family:'Courier New',monospace;font-size:12px;max-width:300px;margin:0 auto;padding:10px;">
    <div style="text-align:center;margin-bottom:10px;">
      ${logoBlock}
      <div style="font-size:18px;font-weight:bold;">${brandName}</div>
      <div style="font-size:11px;color:#666;">Kitchen Order Ticket</div>
      ${tableLine}
      <div style="font-size:11px;color:#666;">${new Date().toLocaleString("en-IN")}</div>
      <div style="font-size:12px;font-weight:bold;margin-top:4px;">KOT for Bill: ${posState.currentBillNo || "---"}</div>
    </div>
    <hr style="border-top:1px dashed #000;margin:6px 0;"/>
    <table style="width:100%;border-collapse:collapse;">
      <thead><tr><th style="text-align:left">Item</th><th style="text-align:center">Qty</th><th style="text-align:right">Status</th></tr></thead>
      <tbody>${lines}</tbody>
    </table>
    <hr style="border-top:1px dashed #000;margin:8px 0;"/>
    <div style="text-align:center;font-size:11px;color:#666;">KITCHEN COPY</div>
  </div>`;
  posPrintNow();
}

function posPrintNow() {
  const wrap = document.getElementById("receiptWrap");
  const content = (wrap?.innerHTML || "").trim();
  if (!content) {
    posNotify("warning", "Nothing to print.");
    return;
  }

  let frame = document.getElementById("posPrintFrame");
  if (!frame) {
    frame = document.createElement("iframe");
    frame.id = "posPrintFrame";
    frame.setAttribute("aria-hidden", "true");
    frame.style.position = "fixed";
    frame.style.right = "0";
    frame.style.bottom = "0";
    frame.style.width = "0";
    frame.style.height = "0";
    frame.style.border = "0";
    document.body.appendChild(frame);
  }

  const printDoc = frame.contentWindow?.document;
  if (!printDoc || !frame.contentWindow) {
    posNotify("error", "Unable to open print preview.");
    return;
  }

  printDoc.open();
  printDoc.write(`<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>Print</title>
  <style>
    @page { size: 80mm auto; margin: 0; }
    html, body { margin: 0; padding: 0; background: #fff; color: #000; }
    body { font-family: 'Courier New', monospace; }
    .print-root { width: 80mm; max-width: 80mm; padding: 2mm; box-sizing: border-box; }
    * { -webkit-print-color-adjust: exact; print-color-adjust: exact; box-shadow: none; text-shadow: none; }
  </style>
</head>
<body>
  <div class="print-root">${content}</div>
<\/body>
<\/html>`);
  printDoc.close();

  setTimeout(() => {
    try {
      frame.contentWindow.focus();
      frame.contentWindow.print();
    } catch {
      posNotify("error", "Print failed. Please try again.");
    }
  }, 60);
}

/* ─── Recall from bill detail / query string ─── */
async function posTryRecallFromQuery() {
  const id = new URLSearchParams(window.location.search).get("recall");
  if (!id) return;
  try {
    const b = await getJSON(`/pos/held-bill/${encodeURIComponent(id)}?outletId=1`);
    if (b.billType && b.billType !== "DineIn") {
      posNotify("warning", "This order is not Dine-In. Open Takeaway POS to continue.");
      return;
    }
    recallBill(b);
    const u = new URL(window.location.href);
    u.searchParams.delete("recall");
    history.replaceState({}, "", u.pathname + u.search + u.hash);
  } catch {
    posNotify("error", "Could not load this bill. It may not be a draft or the link is invalid.");
  }
}

async function posTryRecallFromPath() {
  const m = window.location.pathname.match(/\/pos\/order\/(\d+)/i);
  if (!m || !m[1]) return;
  const id = m[1];
  try {
    const b = await getJSON(`/pos/held-bill/${encodeURIComponent(id)}?outletId=1`);
    if (b.billType && b.billType !== "DineIn") {
      posNotify("warning", "This order is not Dine-In. Open Takeaway POS to continue.");
      return;
    }
    recallBill(b);
  } catch {
    posNotify("error", "Could not load this order.");
  }
}

function posTryPreselectTableFromQuery() {
  const query = new URLSearchParams(window.location.search);
  const tableName = query.get("table");
  const tableId = query.get("tableId");
  if (!tableName) return;
  posState.selectedTableName = tableName;
  posState.selectedTableId = tableId ? parseInt(tableId, 10) : null;
  updateCartHeader();
}

/* ─── Boot ─── */
document.addEventListener("DOMContentLoaded", () => {
  if (document.getElementById("posRoot")) {
    const autoSavedBillNo = sessionStorage.getItem(POS_AUTOSAVE_TOAST_KEY);
    if (autoSavedBillNo) {
      posNotify("info", `Draft auto-saved (${autoSavedBillNo}).`);
      sessionStorage.removeItem(POS_AUTOSAVE_TOAST_KEY);
    }

    window.addEventListener("beforeunload", (e) => {
      if (!hasUnsavedOrderChanges()) return;
      saveDraftOnUnload();
      e.preventDefault();
      e.returnValue = "";
    });

    document.addEventListener("visibilitychange", () => {
      if (document.visibilityState !== "hidden") return;
      if (!hasUnsavedOrderChanges()) return;
      saveDraftOnUnload();
    });

    void (async () => {
      await posInit();
      posTryPreselectTableFromQuery();
      await posTryRecallFromPath();
      await posTryRecallFromQuery();
    })();
  }
});
