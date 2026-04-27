/* ─── Theme Toggle ─── */
(function () {
  const saved = localStorage.getItem('theme') || 'light';
  document.documentElement.setAttribute('data-theme', saved);
  window.__theme = saved;
})();

function toggleTheme() {
  const current = document.documentElement.getAttribute('data-theme');
  const next = current === 'dark' ? 'light' : 'dark';
  document.documentElement.setAttribute('data-theme', next);
  localStorage.setItem('theme', next);
  window.__theme = next;
  const icon = document.getElementById('themeIcon');
  if (icon) icon.className = next === 'dark' ? 'fas fa-sun' : 'fas fa-moon';
}

/* ─── Live Clock ─── */
function startClock() {
  const el = document.getElementById('liveClock');
  if (!el) return;
  const update = () => {
    const n = new Date();
    el.textContent = n.toLocaleString('en-IN', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true
    });
  };
  update(); setInterval(update, 1000);
}

/* ─── Sidebar Toggle ─── */
function initSidebar() {
  const sidebar = document.getElementById('appSidebar');
  const toggle = document.getElementById('sidebarToggle');
  if (!sidebar || !toggle) return;
  const isMobile = window.matchMedia('(max-width: 768px)').matches;
  const saved = !isMobile && localStorage.getItem('sidebarCollapsed') === 'true';
  if (saved) sidebar.classList.add('collapsed');

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

  try {
    const settings = await getJSON('/settings/get?outletId=1');
    window.__branding = settings || {};
    const name = (settings?.restaurantName || '').trim();
    const logoUrl = (settings?.logoUrl || '').trim();

    brandName.textContent = name || 'RestoBill';
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
  if (window.__branding && (window.__branding.restaurantName || window.__branding.logoUrl)) {
    return window.__branding;
  }
  try {
    const settings = await getJSON('/settings/get?outletId=1');
    window.__branding = settings || {};
    return window.__branding;
  } catch {
    return { restaurantName: "RestoBill", logoUrl: "" };
  }
};

/* ─── Confirm modal ─── */
function confirmAction(msg, onYes) {
  if (confirm(msg)) onYes();
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
  initAllDataTables();
});
