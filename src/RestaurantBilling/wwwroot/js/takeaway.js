const twState = {
  categories: [], items: [], cart: [],
  searchTerm: "", selectedCategoryId: null, payMethod: "Cash", partialSplitEnabled: false,
  currentBillId: null, currentBillNo: null, billLevelDiscount: 0, manualGrandTotal: null, hasPendingKot: false
};
const TW_AUTOSAVE_TOAST_KEY = "tw.autosave.toast";

const twFmtQty = (value) => {
  const num = Number(value);
  if (!Number.isFinite(num)) return "0";
  return num.toFixed(3).replace(/\.?0+$/, "");
};

function twClearManualGrandTotal() {
  twState.manualGrandTotal = null;
}

function twHasUnsavedOrderChanges() {
  return twState.cart.length > 0 && (twState.hasPendingKot || !twState.currentBillId);
}

function twSaveDraftOnUnload() {
  if (!twState.cart.length) return;
  if (twState.currentBillId) return;

  const payload = {
    outletId: 1,
    billType: "Takeaway",
    businessDate: new Date().toISOString().slice(0, 10),
    items: twState.cart.map(x => ({
      itemId: x.itemId,
      itemName: x.name,
      qty: x.qty,
      rate: x.price,
      discountAmount: 0,
      taxPercent: x.taxPercent || 5,
      isTaxInclusive: false,
      taxType: "GST"
    })),
    billLevelDiscount: twState.billLevelDiscount || 0,
    serviceChargeOptIn: false,
    serviceChargeAmount: 0
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
          sessionStorage.setItem(TW_AUTOSAVE_TOAST_KEY, String(json.billNo));
        }
      })
      .catch(() => { });
  } catch {
    return Promise.resolve();
  }
}

function twUpdateHeaderLabel() {
  const title = document.getElementById("twHeaderTitle");
  if (!title) return;
  title.textContent = twState.currentBillNo ? `Order #${twState.currentBillNo}` : "Order #";
}

async function twPrintKotSlip() {
  const wrap = document.getElementById("twReceiptWrap");
  if (!wrap) {
    twPrintNow();
    return;
  }
  const lines = twState.cart.map((l) => `
      <tr>
        <td>${l.name}</td>
      <td style="text-align:center">${twFmtQty(l.qty)}</td>
      </tr>`).join("");
  const branding = typeof window.getBrandingSettings === "function"
    ? await window.getBrandingSettings()
    : { restaurantName: "RestoBill", logoUrl: "" };
  const brandName = (branding?.restaurantName || "RestoBill");
  const safeLogoUrl = String(branding?.logoUrl || "").trim();
  const logoBlock = safeLogoUrl && !safeLogoUrl.includes("${")
    ? `<img src="${safeLogoUrl}" alt="Logo" style="max-height:48px;max-width:120px;object-fit:contain;margin:0 auto 4px;display:block;" />`
    : "";
  wrap.innerHTML = `<div style="font-family:'Courier New',monospace;font-size:12px;max-width:300px;margin:0 auto;padding:10px;">
      <div style="text-align:center;margin-bottom:10px;">
        ${logoBlock}
        <div style="font-size:18px;font-weight:bold;">${brandName}</div>
        <div style="font-size:11px;color:#666;">Kitchen Order Ticket</div>
        <div style="font-size:11px;color:#666;">Takeaway</div>
        <div style="font-size:11px;color:#666;">${new Date().toLocaleString("en-IN")}</div>
        <div style="font-size:12px;font-weight:bold;margin-top:4px;">KOT for Bill: ${twState.currentBillNo || "---"}</div>
      </div>
      <hr style="border-top:1px dashed #000;margin:6px 0;"/>
      <table style="width:100%;border-collapse:collapse;">
        <thead><tr><th style="text-align:left">Item</th><th style="text-align:center">Qty</th></tr></thead>
        <tbody>${lines}</tbody>
      </table>
      <hr style="border-top:1px dashed #000;margin:8px 0;"/>
      <div style="text-align:center;font-size:11px;color:#666;">KITCHEN COPY</div>
    </div>`;
  twPrintNow();
}

function twPrintNow() {
  const wrap = document.getElementById("twReceiptWrap");
  const content = (wrap?.innerHTML || "").trim();
  if (!content) {
    if (typeof toastr !== "undefined") toastr.warning("Nothing to print.");
    return;
  }

  let frame = document.getElementById("twPrintFrame");
  if (!frame) {
    frame = document.createElement("iframe");
    frame.id = "twPrintFrame";
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
    if (typeof toastr !== "undefined") toastr.error("Unable to open print preview.");
    return;
  }

  const htmlDoc = [
    "<!doctype html>",
    "<html>",
    "<head>",
    "  <meta charset=\"utf-8\" />",
    "  <title>Takeaway Print</title>",
    "  <style>",
    "    @page { size: 80mm auto; margin: 0; }",
    "    html, body { margin: 0; padding: 0; background: #fff; color: #000; }",
    "    body { font-family: 'Courier New', monospace; }",
    "    .print-root { width: 80mm; max-width: 80mm; padding: 2mm; box-sizing: border-box; }",
    "    * { -webkit-print-color-adjust: exact; print-color-adjust: exact; box-shadow: none; text-shadow: none; }",
    "  </style>",
    "</head>",
    "<body>",
    `  <div class="print-root">${content}</div>`,
    "</body>",
    "</html>"
  ].join("\n");

  printDoc.open();
  printDoc.write(htmlDoc);
  printDoc.close();

  setTimeout(() => {
    try {
      frame.contentWindow.focus();
      frame.contentWindow.print();
    } catch {
      if (typeof toastr !== "undefined") toastr.error("Print failed. Please try again.");
    }
  }, 60);
}

function twUpdateKotActionButtons() {
  const canSendKot = twState.hasPendingKot && twState.cart.length > 0;
  const canPrintKot = twState.cart.length > 0;
  const btnKot = document.getElementById("twBtnKot");
  const btnKotPrint = document.getElementById("twBtnKotPrint");
  const status = document.getElementById("twKotStatus");
  const hint = document.getElementById("twCartKotHint");
  if (btnKot) btnKot.disabled = !canSendKot;
  if (btnKotPrint) {
    btnKotPrint.disabled = !canPrintKot;
    if (canPrintKot) btnKotPrint.removeAttribute("disabled");
  }
  if (hint) hint.style.display = canSendKot ? "" : "none";
  if (status) {
    status.textContent = canSendKot ? "Pending KOT" : "KOT Sent";
    status.classList.toggle("sent", !canSendKot);
  }
}

function twMarkPendingKot() {
  twState.hasPendingKot = twState.cart.length > 0;
  twUpdateKotActionButtons();
}

async function twInit() {
  twBindPosItemGrid();
  await twLoadCatalog();
  twRenderAll();
  twBindEvents();
  twUpdateHeaderLabel();
  await twTryRecallFromQuery();
  twTryAutoSettleFromQuery();
}

function twBindPosItemGrid() {
  const host = document.getElementById("twItems");
  if (!host || host.dataset.twGridBound === "1") return;
  host.dataset.twGridBound = "1";
  host.addEventListener("click", (e) => {
    const target = e.target instanceof Element ? e.target : e.target?.parentElement;
    const tile = target?.closest(".item-tile");
    if (!tile || !host.contains(tile)) return;
    const raw = tile.getAttribute("data-item-id");
    if (raw == null || raw === "") return;
    const item = twGetItemFromTile(raw, tile);
    if (item) {
      e.preventDefault();
      twAdd(item);
    }
  });
}

function twGetItemFromTile(rawId, tile) {
  const idNum = Number(rawId);
  let item = Number.isFinite(idNum)
    ? twState.items.find((x) => Number(x.id) === idNum)
    : twState.items.find((x) => String(x.id) === String(rawId));
  if (!item) {
    const name = (tile?.getAttribute("data-item-name") || "").trim().toLowerCase();
    if (name) item = twState.items.find((x) => String(x.name || "").trim().toLowerCase() === name);
  }
  return item || null;
}

async function twLoadCatalog() {
  try {
    const data = await getJSON("/pos/catalog?outletId=1");
    twState.categories = (data.categories || []).map(c => ({ id: c.id, name: c.name, color: c.color }));
    twState.items = (data.items || []).map(i => ({ id: i.id, name: i.name, categoryId: i.categoryId, price: i.price, taxPercent: i.taxPercent || 0, foodType: i.foodType || "veg", imageUrl: i.imageUrl || "" }));
  } catch {
    if (typeof toastr !== "undefined") toastr.error("Unable to load catalog.");
  }
}

function twRenderAll() { twRenderCats(); twRenderItems(); twRenderCart(); }

function twRenderCats() {
  const host = document.getElementById("twCategoryTabs") || document.getElementById("twCategories");
  if (!host) return;
  host.innerHTML = "";
  const allEl = document.createElement("div");
  allEl.className = "cat-item" + (twState.selectedCategoryId === null ? " active" : "");
  allEl.innerHTML = `<span class="cat-dot" style="background:#94A3B8"></span>All Items`;
  allEl.onclick = () => { twState.selectedCategoryId = null; twRenderCats(); twRenderItems(); };
  host.appendChild(allEl);
  twState.categories.forEach(c => {
    const el = document.createElement("div");
    el.className = "cat-item" + (twState.selectedCategoryId === c.id ? " active" : "");
    el.innerHTML = `<span class="cat-dot" style="background:${c.color || "#18181b"}"></span>${c.name}`;
    el.onclick = () => { twState.selectedCategoryId = c.id; twRenderCats(); twRenderItems(); };
    host.appendChild(el);
  });
}

function twRenderItems() {
  const host = document.getElementById("twItems");
  const cnt = document.getElementById("twItemsCount");
  if (!host) return;
  const filtered = twState.items.filter(i =>
    (!twState.selectedCategoryId || i.categoryId === twState.selectedCategoryId) &&
    (!twState.searchTerm || i.name.toLowerCase().includes(twState.searchTerm))
  );
  if (cnt) cnt.textContent = filtered.length ? `${filtered.length} items` : "";
  host.innerHTML = "";
  if (!filtered.length) {
    host.innerHTML = `<div style="grid-column:1/-1;text-align:center;padding:48px 20px;color:var(--text-muted);"><i class="fas fa-search" style="font-size:32px;opacity:0.2;display:block;margin-bottom:10px;"></i>No items found</div>`;
    return;
  }
  filtered.forEach((i) => {
    const tile = document.createElement("div");
    tile.className = "item-tile";
    tile.setAttribute("data-item-id", String(i.id));
    tile.setAttribute("data-item-name", String(i.name || ""));
    tile.setAttribute("role", "button");
    tile.setAttribute("tabindex", "0");
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
    tile.addEventListener("keydown", (ev) => {
      if (ev.key === "Enter" || ev.key === " ") {
        ev.preventDefault();
        twAdd(i);
      }
    });
    host.appendChild(tile);
  });
}

function twAdd(item) {
  const ex = twState.cart.find((x) => x.itemId == item.id);
  if (ex) ex.qty++;
  else twState.cart.push({ itemId: item.id, name: item.name, price: item.price, taxPercent: item.taxPercent || 0, qty: 1 });
  twClearManualGrandTotal();
  twMarkPendingKot();
  twRenderCart();
}

function twGetTotals() {
  let subtotal = 0;
  const lines = twState.cart.map((line) => {
    const lineSub = line.qty * line.price;
    subtotal += lineSub;
    return { lineSub, lineTaxRate: (line.taxPercent || 0) / 100 };
  });
  const discount = Math.max(0, Math.min(twState.billLevelDiscount || 0, subtotal));
  let tax = 0;
  lines.forEach((l) => {
    const proportionalDiscount = subtotal > 0 ? (discount * l.lineSub) / subtotal : 0;
    tax += (l.lineSub - proportionalDiscount) * l.lineTaxRate;
  });
  const rawGrand = subtotal - discount + tax;
  const autoGrand = Math.round(rawGrand);
  const grand = Number.isInteger(twState.manualGrandTotal) && twState.manualGrandTotal >= 0
    ? twState.manualGrandTotal
    : autoGrand;
  const roundOff = grand - rawGrand;
  return { subtotal, tax, discount, rawGrand, roundOff, grand };
}

function twCalcGrand() {
  return twGetTotals().grand;
}

function twRenderCart() {
  const host = document.getElementById("twCartItems");
  if (!host) return;
  if (!twState.cart.length) {
    host.innerHTML = `<div class="cart-empty"><div class="cart-empty-icon"><i class="fas fa-shopping-bag"></i></div><p>Cart is empty</p><span>Click items to add</span></div>`;
    ["twSubTotal", "twTaxTotal", "twRoundOff", "twGrandTotal"].forEach(id => { const el = document.getElementById(id); if (el) el.textContent = fmtINR(0); });
    const disEl = document.getElementById("twDiscountTotal");
    if (disEl) disEl.textContent = fmtINR(0);
    twUpdateKotActionButtons();
    return;
  }
  let sub = 0;
  host.innerHTML = twState.cart.map((l, idx) => {
    const ls = l.qty * l.price;
    sub += ls;
    return `<div class="cart-item">
        <div class="cart-item-info"><div class="cart-item-name">${l.name}</div><div class="cart-item-sub">${fmtINR(l.price)} each</div></div>
        <div class="cart-qty-ctrl">
          <button class="qty-btn minus" onclick="twQty(${idx},-1)">&#8722;</button>
          <span class="qty-val">${l.qty}</span>
          <button class="qty-btn" onclick="twQty(${idx},1)">&#43;</button>
        </div>
        <div class="cart-item-total">${fmtINR(ls)}</div>
        <span class="cart-remove" onclick="twRemove(${idx})"><i class="fas fa-xmark"></i></span>
      </div>`;
  }).join("");
  const totals = twGetTotals();
  document.getElementById("twSubTotal").textContent = fmtINR(sub);
  document.getElementById("twTaxTotal").textContent = fmtINR(totals.tax);
  document.getElementById("twRoundOff").textContent = fmtINR(totals.roundOff);
  document.getElementById("twGrandTotal").textContent = fmtINR(totals.grand);
  const disEl = document.getElementById("twDiscountTotal");
  if (disEl) disEl.textContent = fmtINR(totals.discount);
  twUpdateKotActionButtons();
}

window.twQty = (idx, d) => {
  twState.cart[idx].qty += d;
  if (twState.cart[idx].qty <= 0) twState.cart.splice(idx, 1);
  twClearManualGrandTotal();
  twMarkPendingKot();
  twRenderCart();
};

window.twRemove = (idx) => {
  twState.cart.splice(idx, 1);
  twClearManualGrandTotal();
  twMarkPendingKot();
  twRenderCart();
};

function twBindEvents() {
  const search = document.getElementById("twSearch");
  if (search) { let t; search.addEventListener("input", e => { clearTimeout(t); t = setTimeout(() => { twState.searchTerm = e.target.value.trim().toLowerCase(); twRenderItems(); }, 250); }); }

  document.getElementById("twBtnKot")?.addEventListener("click", twGenerateKot);
  document.getElementById("twBtnKotPrint")?.addEventListener("click", twGenerateKotAndPrint);
  document.getElementById("twBtnSettle")?.addEventListener("click", twOpenSettle);
  document.getElementById("twBtnEditDiscount")?.addEventListener("click", () => {
    const subtotal = twState.cart.reduce((s, x) => s + x.qty * x.price, 0);
    if (!subtotal) {
      if (typeof toastr !== "undefined") toastr.info("Add items first.");
      return;
    }
    const current = Number(twState.billLevelDiscount || 0).toFixed(2);
    const raw = prompt(`Enter discount amount (max ${fmtINR(subtotal)})`, current);
    if (raw === null) return;
    const next = Number(raw);
    if (!Number.isFinite(next) || next < 0) {
      if (typeof toastr !== "undefined") toastr.warning("Invalid discount amount.");
      return;
    }
    twState.billLevelDiscount = Math.min(next, subtotal);
    twClearManualGrandTotal();
    twRenderCart();
  });
  document.getElementById("twBtnEditRoundOff")?.addEventListener("click", () => {
    if (!twState.cart.length) {
      if (typeof toastr !== "undefined") toastr.info("Add items first.");
      return;
    }
    const totals = twGetTotals();
    const defaultGrand = Number.isInteger(twState.manualGrandTotal) ? twState.manualGrandTotal : totals.grand;
    const raw = prompt("Enter final payable amount (whole number)", String(defaultGrand));
    if (raw === null) return;
    const next = Number(raw);
    if (!Number.isFinite(next) || next < 0) {
      if (typeof toastr !== "undefined") toastr.warning("Invalid total amount.");
      return;
    }
    twState.manualGrandTotal = Math.round(next);
    twRenderCart();
  });
  document.getElementById("twBtnClear")?.addEventListener("click", () => {
    twState.cart = [];
    twState.currentBillId = null;
    twState.currentBillNo = null;
    twState.billLevelDiscount = 0;
    twClearManualGrandTotal();
    twState.hasPendingKot = false;
    twRenderCart();
    twUpdateHeaderLabel();
  });
  document.getElementById("twSettleClose")?.addEventListener("click", twCloseSettle);
  document.getElementById("twSettleCancel")?.addEventListener("click", twCloseSettle);
  document.getElementById("twSettleConfirm")?.addEventListener("click", twConfirmSettle);
  document.getElementById("twSettleOverlay")?.addEventListener("click", e => { if (e.target.id === "twSettleOverlay") twCloseSettle(); });

  document.querySelectorAll(".settle-pay-btn[data-tw-method]").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".settle-pay-btn[data-tw-method]").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      twState.payMethod = btn.dataset.twMethod;
      const splitToggle = document.getElementById("twPartialSplit");
      const splitRow = splitToggle?.closest(".settle-split-row");
      const partialCashGroup = document.getElementById("twPartialCashGroup");
      const isCashOnly = (twState.payMethod || "Cash") === "Cash";
      if (splitToggle) {
        splitToggle.disabled = isCashOnly;
        if (isCashOnly) {
          splitToggle.checked = false;
          twState.partialSplitEnabled = false;
        }
      }
      splitRow?.classList.toggle("disabled", isCashOnly);
      if (partialCashGroup) partialCashGroup.style.display = (!isCashOnly && splitToggle?.checked) ? "" : "none";
      twUpdatePartialPaymentUi();
    });
  });

  const twUpdatePartialPaymentUi = () => {
    const grand = twCalcGrand();
    const splitToggle = document.getElementById("twPartialSplit");
    const partialCashGroup = document.getElementById("twPartialCashGroup");
    const partialCashInput = document.getElementById("twPartialCashInput");
    const partialSelectedInput = document.getElementById("twPartialSelectedInput");
    const partialSelectedLabel = document.getElementById("twPartialSelectedLabel");
    const partialRemaining = document.getElementById("twPartialRemaining");
    const isPartial = Boolean(splitToggle?.checked) && (twState.payMethod || "Cash") !== "Cash";
    if (partialCashGroup) partialCashGroup.style.display = isPartial ? "" : "none";
    if (!isPartial) {
      twState.partialSplitEnabled = false;
      if (partialCashInput) partialCashInput.value = "";
      if (partialSelectedInput) partialSelectedInput.value = "";
      if (partialRemaining) partialRemaining.textContent = `Remaining in selected mode: ${fmtINR(grand)}`;
      return;
    }
    twState.partialSplitEnabled = true;
    const selectedMethod = twState.payMethod || "Card";
    if (partialSelectedLabel) partialSelectedLabel.textContent = `${selectedMethod} Amount`;
    const cash = Math.max(0, Number.parseFloat(partialCashInput?.value || "0") || 0);
    const clampedCash = Math.min(cash, grand);
    if (partialCashInput && cash !== clampedCash) partialCashInput.value = clampedCash.toFixed(2);
    const remaining = Math.max(0, grand - clampedCash);
    if (partialSelectedInput) partialSelectedInput.value = remaining.toFixed(2);
    if (partialRemaining) partialRemaining.textContent = `Remaining in selected mode: ${fmtINR(remaining)}`;
  };

  const splitToggle = document.getElementById("twPartialSplit");
  splitToggle?.addEventListener("change", () => {
    const checked = Boolean(splitToggle.checked);
    if (checked && (twState.payMethod || "Cash") === "Cash") {
      splitToggle.checked = false;
      twState.partialSplitEnabled = false;
      if (typeof toastr !== "undefined") toastr.info("Choose Card or UPI to enable partial split with cash.");
      return;
    }
    twState.partialSplitEnabled = checked;
    twUpdatePartialPaymentUi();
  });

  document.getElementById("twPartialCashInput")?.addEventListener("input", twUpdatePartialPaymentUi);

  const amtInput = document.getElementById("twAmtInput");
  const amtEditable = document.getElementById("twAmtEditable");
  amtEditable?.addEventListener("change", () => {
    if (!amtInput) return;
    amtInput.readOnly = !amtEditable.checked;
  });
  if (amtInput) amtInput.addEventListener("input", () => {
    const change = parseFloat(amtInput.value || 0) - twCalcGrand();
    const row = document.getElementById("twChangeRow");
    const due = document.getElementById("twChangeDue");
    if (row && due) { row.style.display = change >= 0 ? "block" : "none"; due.textContent = fmtINR(Math.max(0, change)); }
  });
}

async function twEnsureHeldBill() {
  const draftPayload = {
    outletId: 1,
    items: twState.cart.map(x => ({
      itemId: x.itemId, itemName: x.name, qty: x.qty, rate: x.price,
      discountAmount: 0, taxPercent: x.taxPercent || 5, isTaxInclusive: false, taxType: "GST"
    })),
    billLevelDiscount: twState.billLevelDiscount || 0,
    serviceChargeOptIn: false,
    serviceChargeAmount: 0,
    customerName: null,
    phone: null
  };
  if (twState.currentBillId) {
    await postJSON(`/pos/update-draft/${twState.currentBillId}`, draftPayload);
    return;
  }
  const held = await postJSON("/pos/hold", {
    ...draftPayload,
    billType: "Takeaway",
    businessDate: new Date().toISOString().slice(0, 10)
  });
  twState.currentBillId = held.billId;
  twState.currentBillNo = held.billNo;
  twUpdateHeaderLabel();
}

async function twGenerateKot() {
  if (!twState.cart.length) {
    if (typeof toastr !== "undefined") toastr.warning("Add items before generating KOT.");
    return;
  }
  const btn = document.getElementById("twBtnKot");
  if (btn) {
    btn.disabled = true;
    btn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Sending...`;
  }
  try {
    await twEnsureHeldBill();
    const result = await postJSON("/kot/generate", {
      outletId: 1, billId: twState.currentBillId, captainUserId: 1
    });
    const msg = result.reused
      ? "KOT already exists for this bill."
      : `KOT sent to kitchen! Bill: ${twState.currentBillNo}`;
    twState.hasPendingKot = false;
    twUpdateKotActionButtons();
    if (typeof toastr !== "undefined") toastr.success(msg);
    return { ok: true, kotIds: Array.isArray(result.kotIds) ? result.kotIds : [] };
  } catch {
    if (typeof toastr !== "undefined") toastr.error("Failed to generate KOT.");
    return { ok: false, kotIds: [] };
  } finally {
    if (btn) {
      btn.innerHTML = `<i class="fas fa-paper-plane"></i> Send KOT`;
    }
    twUpdateKotActionButtons();
  }
}

async function twGenerateKotAndPrint() {
  const btn = document.getElementById("twBtnKotPrint");
  if (btn) {
    btn.disabled = true;
    btn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Printing...`;
  }
  try {
    const result = await twGenerateKot();
    if (result?.ok) {
      if (result.kotIds.length) {
        try {
          await postJSON("/kot/mark-printed", { outletId: 1, kotIds: result.kotIds });
        } catch {
        }
      }
      await twPrintKotSlip();
    }
  } finally {
    if (btn) {
      btn.innerHTML = `<i class="fas fa-print"></i> KOT & print`;
      btn.disabled = false;
      btn.removeAttribute("disabled");
    }
    twUpdateKotActionButtons();
  }
}

function twOpenSettle() {
  if (!twState.cart.length) { if (typeof toastr !== "undefined") toastr.warning("Cart is empty."); return; }
  const totals = twGetTotals();
  const grand = totals.grand;
  const sub = totals.subtotal;
  const tax = totals.tax;
  const roundOff = totals.roundOff;
  const discount = totals.discount;
  const summaryEl = document.getElementById("twSettleSummary");
  if (summaryEl) summaryEl.innerHTML = `
      ${twState.cart.map(l => `<div class="settle-summary-row"><span>${l.name} x${twFmtQty(l.qty)}</span><span>${fmtINR(l.qty * l.price)}</span></div>`).join("")}
      <div class="settle-summary-row" style="border-top:1px solid var(--border);margin-top:6px;padding-top:6px;"><span>Subtotal</span><span>${fmtINR(sub)}</span></div>
      <div class="settle-summary-row"><span>Discount</span><span>${fmtINR(discount)}</span></div>
      <div class="settle-summary-row"><span>Tax</span><span>${fmtINR(tax)}</span></div>
      <div class="settle-summary-row"><span>Round Off</span><span>${fmtINR(roundOff)}</span></div>
      <div class="settle-summary-row grand"><span>Grand Total</span><span>${fmtINR(grand)}</span></div>`;
  document.getElementById("twAmtInput").value = String(grand);
  document.getElementById("twChangeRow").style.display = "none";
  const twPartialCashGroup = document.getElementById("twPartialCashGroup");
  const twPartialCashInput = document.getElementById("twPartialCashInput");
  const twPartialSelectedInput = document.getElementById("twPartialSelectedInput");
  const twPartialSelectedLabel = document.getElementById("twPartialSelectedLabel");
  const twPartialRemaining = document.getElementById("twPartialRemaining");
  const twAmtEditable = document.getElementById("twAmtEditable");
  const splitToggle = document.getElementById("twPartialSplit");
  if (splitToggle) {
    const isCashOnly = (twState.payMethod || "Cash") === "Cash";
    splitToggle.checked = false;
    splitToggle.disabled = isCashOnly;
    splitToggle.closest(".settle-split-row")?.classList.toggle("disabled", isCashOnly);
  }
  if (twPartialCashGroup) twPartialCashGroup.style.display = "none";
  if (twPartialCashInput) twPartialCashInput.value = "";
  if (twPartialSelectedInput) twPartialSelectedInput.value = "";
  if (twPartialSelectedLabel) twPartialSelectedLabel.textContent = `${twState.payMethod || "Card"} Amount`;
  if (twPartialRemaining) twPartialRemaining.textContent = `Remaining in selected mode: ${fmtINR(grand)}`;
  twState.partialSplitEnabled = false;
  if (twAmtEditable) twAmtEditable.checked = false;
  if (document.getElementById("twAmtInput")) document.getElementById("twAmtInput").readOnly = true;
  document.getElementById("twSettleOverlay").classList.add("show");
}

function twCloseSettle() { document.getElementById("twSettleOverlay")?.classList.remove("show"); }

async function twTryRecallFromQuery() {
  const id = new URLSearchParams(window.location.search).get("recall");
  if (!id) return;
  try {
    const b = await getJSON(`/pos/held-bill/${encodeURIComponent(id)}?outletId=1`);
    if (b.billType && b.billType !== "Takeaway") {
      if (typeof toastr !== "undefined") toastr.warning("This order is not Takeaway. Open Dine-In POS to continue.");
      return;
    }
    twState.currentBillId = b.billId;
    twState.currentBillNo = b.billNo;
    twState.billLevelDiscount = Number(b.discountAmount || 0);
    twClearManualGrandTotal();
    if (typeof b.hasPendingKot === "boolean") twState.hasPendingKot = b.hasPendingKot;
    else if (typeof b.hasAnyKot === "boolean") twState.hasPendingKot = !b.hasAnyKot;
    else twState.hasPendingKot = true;
    twState.cart = (b.items || []).map(i => ({
      itemId: i.itemId, name: i.name, price: i.rate, taxPercent: i.taxPercent || 5, qty: i.qty
    }));
    twRenderCart();
    twUpdateHeaderLabel();
    if (typeof toastr !== "undefined") toastr.info("Bill " + b.billNo + " loaded.");
    const u = new URL(window.location.href);
    u.searchParams.delete("recall");
    history.replaceState({}, "", u.pathname + u.search + u.hash);
  } catch {
    if (typeof toastr !== "undefined") toastr.error("Could not load this bill. It may not be a draft.");
  }
}

function twTryAutoSettleFromQuery() {
  const params = new URLSearchParams(window.location.search);
  if (params.get("autosettle") !== "1") return;
  if (!twState.currentBillId || !twState.cart.length) return;
  twOpenSettle();
  const u = new URL(window.location.href);
  u.searchParams.delete("autosettle");
  history.replaceState({}, "", u.pathname + u.search + u.hash);
}

async function twConfirmSettle() {
  const grand = twCalcGrand();
  const splitToggle = document.getElementById("twPartialSplit");
  const isPartialSplit = Boolean(splitToggle?.checked) && (twState.payMethod || "Cash") !== "Cash";
  const twPartialCashInput = document.getElementById("twPartialCashInput");
  const payments = (() => {
    if (!isPartialSplit) return [{ mode: twState.payMethod, amount: grand }];
    const enteredCash = Math.max(0, Number.parseFloat(twPartialCashInput?.value || "0") || 0);
    const cashAmount = Math.min(Math.round(enteredCash), grand);
    const digitalAmount = grand - cashAmount;
    return [
      { mode: "Cash", amount: cashAmount },
      { mode: twState.payMethod, amount: digitalAmount }
    ];
  })();
  const paidFromSplit = payments.reduce((sum, p) => sum + Number(p.amount || 0), 0);
  const btn = document.getElementById("twSettleConfirm");
  if (btn) { btn.disabled = true; btn.innerHTML = `<i class="fas fa-spinner fa-spin"></i> Processing...`; }
  try {
    let result;
    if (twState.currentBillId) {
      result = await postJSON(`/pos/settle-existing/${twState.currentBillId}`, {
        outletId: 1,
        payments
      });
    } else {
      result = await postJSON("/pos/settle", {
        outletId: 1, billType: "Takeaway",
        businessDate: new Date().toISOString().slice(0, 10),
        isInterState: false, billLevelDiscount: twState.billLevelDiscount || 0,
        serviceChargeOptIn: false, serviceChargeAmount: 0,
        items: twState.cart.map(x => ({
          itemId: x.itemId, itemName: x.name, qty: x.qty, rate: x.price,
          discountAmount: 0, taxPercent: x.taxPercent || 5,
          isTaxInclusive: false, taxType: "GST", isStockTracked: false
        })),
        payments
      });
    }
    twCloseSettle();
    const amtInput = parseFloat(document.getElementById("twAmtInput")?.value || paidFromSplit);
    const amtPaid = Number.isFinite(amtInput) ? amtInput : paidFromSplit;
    const change = Math.max(0, amtPaid - grand);
    const totals = twGetTotals();
    const sub = totals.subtotal;
    const taxAmt = totals.tax;
    const discAmt = totals.discount;
    const roundOffAmt = totals.roundOff;
    const branding = typeof window.getBrandingSettings === "function"
      ? await window.getBrandingSettings()
      : { restaurantName: "RestoBill", logoUrl: "" };
    const brandName = (branding?.restaurantName || "RestoBill");
    const safeLogoUrl = String(branding?.logoUrl || "").trim();
    const logoBlock = safeLogoUrl && !safeLogoUrl.includes("${")
      ? `<img src="${safeLogoUrl}" alt="Logo" style="max-height:48px;max-width:120px;object-fit:contain;margin:0 auto 4px;display:block;" />`
      : "";
    const receiptHtml = `<div style="font-family:'Courier New',monospace;font-size:12px;max-width:300px;margin:0 auto;padding:10px;">
        <div style="text-align:center;margin-bottom:10px;">${logoBlock}<div style="font-size:18px;font-weight:bold;">${brandName}</div><div style="font-size:11px;color:#666;">Takeaway Receipt</div><div style="font-size:11px;color:#666;">${new Date().toLocaleString("en-IN")}</div><div style="font-size:12px;font-weight:bold;margin-top:4px;">Bill: ${result.billNo || "---"}</div></div>
        <hr style="border-top:1px dashed #000;margin:6px 0;"/>
        <table style="width:100%;border-collapse:collapse;"><thead><tr><th style="text-align:left">Item</th><th>Qty</th><th style="text-align:right">Amt</th></tr></thead>
        <tbody>${twState.cart.map(l => `<tr><td>${l.name}</td><td style="text-align:center">${twFmtQty(l.qty)}</td><td style="text-align:right">${fmtINR(l.qty * l.price)}</td></tr>`).join("")}</tbody></table>
        <hr style="border-top:1px dashed #000;margin:6px 0;"/>
        <div style="display:flex;justify-content:space-between;"><span>Subtotal</span><span>${fmtINR(sub)}</span></div>
        <div style="display:flex;justify-content:space-between;"><span>Discount</span><span>${fmtINR(discAmt)}</span></div>
        <div style="display:flex;justify-content:space-between;"><span>Tax</span><span>${fmtINR(taxAmt)}</span></div>
        <div style="display:flex;justify-content:space-between;"><span>Round Off</span><span>${fmtINR(roundOffAmt)}</span></div>
        <div style="display:flex;justify-content:space-between;font-weight:bold;font-size:14px;margin-top:4px;"><span>TOTAL</span><span>${fmtINR(grand)}</span></div>
        ${payments.map(p => `<div style="display:flex;justify-content:space-between;margin-top:4px;"><span>Paid (${p.mode})</span><span>${fmtINR(p.amount || 0)}</span></div>`).join("")}
        ${change > 0 ? `<div style="display:flex;justify-content:space-between;"><span>Change</span><span>${fmtINR(change)}</span></div>` : ""}
        <hr style="border-top:1px dashed #000;margin:8px 0;"/><div style="text-align:center;font-size:11px;color:#666;">Thank you! Visit again.</div></div>`;
    document.getElementById("twReceiptWrap").innerHTML = receiptHtml;
    twPrintNow();
    twState.cart = [];
    twState.billLevelDiscount = 0;
    twClearManualGrandTotal();
    twState.hasPendingKot = false;
    twRenderCart();
    if (typeof toastr !== "undefined") toastr.success(`Takeaway bill ${result.billNo || ""} settled!`);
    twState.currentBillId = null; twState.currentBillNo = null;
    twUpdateHeaderLabel();
  } catch (_e) {
    if (typeof toastr !== "undefined") toastr.error("Settlement failed. Please check items and try again.");
  } finally {
    if (btn) { btn.disabled = false; btn.innerHTML = `<i class="fas fa-check-circle"></i> Confirm & Print`; }
  }
}

document.addEventListener("DOMContentLoaded", twInit);
document.addEventListener("DOMContentLoaded", () => {
  const autoSavedBillNo = sessionStorage.getItem(TW_AUTOSAVE_TOAST_KEY);
  if (autoSavedBillNo) {
    if (typeof toastr !== "undefined") toastr.info(`Draft auto-saved (${autoSavedBillNo}).`);
    sessionStorage.removeItem(TW_AUTOSAVE_TOAST_KEY);
  }
});
window.addEventListener("beforeunload", (e) => {
  if (!twHasUnsavedOrderChanges()) return;
  twSaveDraftOnUnload();
  e.preventDefault();
  e.returnValue = "";
});

document.addEventListener("visibilitychange", () => {
  if (document.visibilityState !== "hidden") return;
  if (!twHasUnsavedOrderChanges()) return;
  twSaveDraftOnUnload();
});
