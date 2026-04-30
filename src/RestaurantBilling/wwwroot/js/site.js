/* ─── Theme Toggle ─── */
(function () {
  const savedTheme = localStorage.getItem('theme');
  const initialTheme = savedTheme === 'light' || savedTheme === 'dark' ? savedTheme : 'dark';
  document.documentElement.setAttribute('data-theme', initialTheme);
  window.__theme = initialTheme;
  localStorage.setItem('theme', initialTheme);
})();

function toggleTheme() {
  const icon = document.getElementById('themeIcon');
  const nextTheme = window.__theme === 'dark' ? 'light' : 'dark';
  document.documentElement.setAttribute('data-theme', nextTheme);
  localStorage.setItem('theme', nextTheme);
  window.__theme = nextTheme;
  if (icon) icon.className = nextTheme === 'dark' ? 'fas fa-sun' : 'fas fa-moon';
}

/* ─── Live Clock ─── */
function startClock() {
  const el = document.getElementById('liveClock');
  if (!el) return;
  const update = () => {
    el.textContent = fmtDateTimeDMY(new Date(), true);
  };
  update(); setInterval(update, 1000);
}

function fmtDateDMY(value) {
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const day = String(d.getDate()).padStart(2, '0');
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const year = d.getFullYear();
  return `${day}/${month}/${year}`;
}

function fmtDateTimeDMY(value, includeSeconds = false) {
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const base = fmtDateDMY(d);
  const hh = d.getHours();
  const h12 = hh % 12 || 12;
  const mm = String(d.getMinutes()).padStart(2, '0');
  const ss = String(d.getSeconds()).padStart(2, '0');
  const meridiem = hh >= 12 ? 'PM' : 'AM';
  const time = includeSeconds ? `${h12}:${mm}:${ss} ${meridiem}` : `${h12}:${mm} ${meridiem}`;
  return `${base} ${time}`;
}
window.fmtDateDMY = fmtDateDMY;
window.fmtDateTimeDMY = fmtDateTimeDMY;

/* ─── Sidebar Toggle ─── */
function initSidebar() {
  const sidebar = document.getElementById('appSidebar');
  const toggle = document.getElementById('sidebarToggle');
  if (!sidebar || !toggle) return;
  const isMobile = window.matchMedia('(max-width: 768px)').matches;
  const saved = !isMobile && localStorage.getItem('sidebarCollapsed') === 'true';
  if (saved) sidebar.classList.add('collapsed');
  // Remove the no-transition init attribute so subsequent toggles animate
  document.documentElement.removeAttribute('data-sidebar-init');

  let overlay = document.querySelector('.sidebar-overlay');
  if (!overlay) {
    overlay = document.createElement('div');
    overlay.className = 'sidebar-overlay';
    document.body.appendChild(overlay);
  }

  const closeMobileSidebar = () => {
    sidebar.classList.remove('mobile-open');
    overlay.classList.remove('show');
  };

  toggle.addEventListener('click', () => {
    if (window.matchMedia('(max-width: 768px)').matches) {
      const willOpen = !sidebar.classList.contains('mobile-open');
      sidebar.classList.toggle('mobile-open');
      overlay.classList.toggle('show', willOpen);
      return;
    }
    sidebar.classList.toggle('collapsed');
    localStorage.setItem('sidebarCollapsed', sidebar.classList.contains('collapsed'));
  });
  overlay.addEventListener('click', closeMobileSidebar);
  const parentLinks = Array.from(document.querySelectorAll('.nav-link-item[data-submenu]'));
  // Keep server-computed open/active state visible after hydration.
  parentLinks.forEach(link => {
    const target = link.getAttribute('data-submenu');
    const sub = target ? document.getElementById(target) : null;
    if (!sub) return;
    const hasActiveChild = !!sub.querySelector('.nav-link-item.active');
    if (hasActiveChild) {
      sub.classList.remove('d-none');
      link.classList.add('open');
    }
  });

  parentLinks.forEach(link => {
    link.addEventListener('click', () => {
      const target = link.getAttribute('data-submenu');
      const sub = document.getElementById(target);
      if (!sub) return;
      const isOpen = !sub.classList.contains('d-none');
      document.querySelectorAll('.sub-nav').forEach(s => s.classList.add('d-none'));
      parentLinks.forEach(l => l.classList.remove('open'));
      if (!isOpen) {
        sub.classList.remove('d-none');
        link.classList.add('open');
      }
    });
  });

  document.querySelectorAll('.nav-link-item[href]').forEach(a => {
    a.addEventListener('click', () => {
      if (window.matchMedia('(max-width: 768px)').matches) {
        closeMobileSidebar();
      }
    });
  });
}

/* ─── Toastr Config ─── */
function initToastr() {
  if (typeof toastr !== 'undefined') {
    toastr.options = {
      closeButton: true, progressBar: true,
      positionClass: 'toast-top-right',
      timeOut: 3500, extendedTimeOut: 1000,
      preventDuplicates: true, newestOnTop: true
    };
  }
  const toastElement = document.getElementById("app-toast");
  if (toastElement && window.bootstrap) {
    try { new window.bootstrap.Toast(toastElement, { delay: 4000 }).show(); } catch (_) {}
  }
}

/* ─── Global notify fallback ─── */
window.notify = function(msg, type) {
  if (typeof toastr !== 'undefined') (toastr[type] || toastr.info)(msg);
};
const metaBrandName = document.querySelector('meta[name="app-brand-default"]')?.getAttribute('content')?.trim();
window.__defaultBrandName = window.__defaultBrandName || metaBrandName || 'RestoBill';

/* ─── Format INR ─── */
function fmtINR(n) {
  return '₹' + parseFloat(n || 0).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/* ─── AJAX helpers ─── */
function postJSON(url, data) {
  return fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Requested-With': 'XMLHttpRequest',
      'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
    },
    body: JSON.stringify(data)
  }).then(r => r.json());
}
function getJSON(url) {
  return fetch(url, {
    cache: 'no-store',
    headers: {
      'X-Requested-With': 'XMLHttpRequest',
      'Cache-Control': 'no-cache',
      Pragma: 'no-cache'
    }
  }).then(r => r.json());
}

/* ─── DataTable factory ─── */
function makeDataTable(selector, opts = {}) {
  if (!window.jQuery || !jQuery.fn.DataTable) return null;
  return $(selector).DataTable(Object.assign({
    pageLength: 25, responsive: true,
    language: { search: '', searchPlaceholder: '🔍 Search...' },
    dom: '<"d-flex justify-content-between align-items-center mb-2"lf>rtip'
  }, opts));
}

function initAllDataTables() {
  if (!window.jQuery || !jQuery.fn.DataTable) return;
  const selectors = [
    '.app-table',
    '.table-custom',
    'table.table'
  ];
  const tables = Array.from(document.querySelectorAll(selectors.join(',')));
  tables.forEach((tbl, idx) => {
    // Skip POS/KDS specific areas.
    if (tbl.closest('.pos-layout') || tbl.closest('.kds-body')) return;
    // DataTables requires strict column count. Skip tables that currently use
    // placeholder rows with colspan (common during async dashboard loading).
    if (tbl.querySelector('tbody td[colspan]')) return;
    if (!tbl.id) tbl.id = `autoTable_${idx + 1}`;
    if ($.fn.dataTable.isDataTable(tbl)) return;
    makeDataTable(`#${tbl.id}`, { pageLength: 10 });
  });
}

function refreshDataTableById(tableId) {
  if (!window.jQuery || !jQuery.fn.DataTable || !tableId) return;
  const table = document.getElementById(tableId);
  if (!table) return;
  if ($.fn.dataTable.isDataTable(table)) {
    $(table).DataTable().destroy();
  }
  makeDataTable(`#${tableId}`, { pageLength: 10 });
}
window.refreshDataTableById = refreshDataTableById;

async function applyBranding() {
  const brandName = document.getElementById('brandNameText');
  const brandLogo = document.getElementById('brandLogoImage');
  const brandFallback = document.getElementById('brandLogoFallback');
  if (!brandName || !brandLogo || !brandFallback) return;

  const normalizeLogoUrl = (value) => {
    const raw = String(value || '').trim();
    if (!raw) return '';
    // Ignore unresolved template placeholders or malformed values from settings.
    if (raw.includes('${') || raw.includes('\n') || raw.includes('\r') || raw.includes('"') || raw.includes("'")) return '';
    if (raw.startsWith('/')) return raw;
    if (/^https?:\/\//i.test(raw)) return raw;
    return '';
  };

  try {
    const settings = await getJSON('/settings/get?outletId=1');
    window.__branding = settings || {};
    const name = (settings?.restaurantName || '').trim();
    const logoUrl = normalizeLogoUrl(settings?.logoUrl);

    brandName.textContent = name || window.__defaultBrandName;
    if (logoUrl) {
      brandLogo.src = logoUrl;
      brandLogo.style.display = 'block';
      brandFallback.style.display = 'none';
    } else {
      brandLogo.style.display = 'none';
      brandFallback.style.display = '';
    }
  } catch (_) {
    // Keep defaults when settings are unavailable.
  }
}

window.getBrandingSettings = async function () {
  const normalizeLogoUrl = (value) => {
    const raw = String(value || '').trim();
    if (!raw) return '';
    if (raw.includes('${') || raw.includes('\n') || raw.includes('\r') || raw.includes('"') || raw.includes("'")) return '';
    if (raw.startsWith('/')) return raw;
    if (/^https?:\/\//i.test(raw)) return raw;
    return '';
  };

  if (window.__branding && (window.__branding.restaurantName || window.__branding.logoUrl)) {
    return {
      ...window.__branding,
      logoUrl: normalizeLogoUrl(window.__branding.logoUrl)
    };
  }
  try {
    const settings = await getJSON('/settings/get?outletId=1');
    window.__branding = {
      ...(settings || {}),
      logoUrl: normalizeLogoUrl(settings?.logoUrl)
    };
    return window.__branding;
  } catch {
    return { restaurantName: window.__defaultBrandName, logoUrl: "" };
  }
};

/* ─── Confirm modal ─── */
function confirmAction(msg, onYes) {
  const modalEl = document.getElementById('globalConfirmModal');
  const messageEl = document.getElementById('globalConfirmMessage');
  const yesBtn = document.getElementById('globalConfirmYes');

  if (!modalEl || !window.bootstrap || !yesBtn) {
    if (confirm(msg)) onYes();
    return;
  }

  if (messageEl) messageEl.textContent = msg || 'Are you sure?';
  const modal = window.bootstrap.Modal.getOrCreateInstance(modalEl);
  const newYesBtn = yesBtn.cloneNode(true);
  yesBtn.parentNode?.replaceChild(newYesBtn, yesBtn);
  newYesBtn.addEventListener('click', () => {
    modal.hide();
    onYes();
  });
  modal.show();
}

/* ─── Manager PIN prompt ─── */
function requireManagerPin(onSuccess) {
  const pin = prompt('Enter Manager PIN to continue:');
  if (pin) {
    postJSON('/admin/verify-pin', { pin })
      .then(r => { if (r.success) onSuccess(); else toastr.error('Invalid PIN'); })
      .catch(() => toastr.error('Server error'));
  }
}

/* ─── Modal safety cleanup (prevents stuck backdrop) ─── */
function initModalSafety() {
  const cleanupModalState = () => {
    const openModals = document.querySelectorAll('.modal.show');
    if (openModals.length > 0) return;

    document.body.classList.remove('modal-open');
    document.body.style.removeProperty('padding-right');
    document.body.style.removeProperty('overflow');
    document.querySelectorAll('.modal-backdrop').forEach(el => el.remove());
  };

  document.addEventListener('hidden.bs.modal', () => {
    // Let Bootstrap complete transition bookkeeping first.
    setTimeout(cleanupModalState, 0);
  });

  document.addEventListener('show.bs.modal', () => {
    // If stale backdrops exist from previous modal flows, clear extras.
    const backdrops = Array.from(document.querySelectorAll('.modal-backdrop'));
    if (backdrops.length > 1) {
      backdrops.slice(0, -1).forEach(el => el.remove());
    }
  });
}

/* ─── Init on DOM ready ─── */
document.addEventListener('DOMContentLoaded', () => {
  startClock();
  initSidebar();
  initToastr();
  applyBranding();
  const icon = document.getElementById('themeIcon');
  if (icon) icon.className = window.__theme === 'dark' ? 'fas fa-sun' : 'fas fa-moon';
  const path = window.location.pathname.toLowerCase();
  document.querySelectorAll('.nav-link-item[href]').forEach(a => {
    const href = a.getAttribute('href');
    if (!href) return;
    if (path.startsWith(href.toLowerCase())) a.classList.add('active');
  });
  initModalSafety();
  initAllDataTables();
});
