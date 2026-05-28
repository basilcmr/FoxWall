let activeTab = 'sockets';
let socketData = [];
let logData = [];
let statusData = { mode: 'Normal', locked: false, rxSpeed: 0, txSpeed: 0, panicActive: false };
let socketFilter = 'all';
let socketSearchQuery = '';
let logFilter = 'all';
let logSearchQuery = '';

// Historical analytics variables
let analyticsRange = '5m';
let analyticsPoints = [];

// Sorting states
let socketSortCol = 'time';
let socketSortAsc = false;
let logSortCol = 'time';
let logSortAsc = false;

// Initialize
window.onload = () => {
  initChart();
  
  // Set default dates and times for mini-calendars (separated date/time pickers)
  const now = new Date();
  const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);
  
  const toLocalDate = (date) => {
    const tzOffset = date.getTimezoneOffset() * 60000;
    return new Date(date.getTime() - tzOffset).toISOString().split('T')[0];
  };

  const toLocalTime = (date) => {
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${hours}:${minutes}`;
  };
  
  document.getElementById('customDateStart').value = toLocalDate(oneHourAgo);
  document.getElementById('customDateEnd').value = toLocalDate(now);
  document.getElementById('customTimeStart').value = toLocalTime(oneHourAgo);
  document.getElementById('customTimeEnd').value = toLocalTime(now);
  
  pollData();
  setInterval(pollData, 1500);
  
  setTimeout(() => {
    lucide.createIcons();
  }, 300);
};

// Switch Sidebar Tabs
function switchTab(tabId, element) {
  activeTab = tabId;
  
  // Update sidebar active state
  document.querySelectorAll('.nav-item').forEach(item => item.classList.remove('active'));
  element.classList.add('active');

  // Show active tab content
  document.querySelectorAll('.tab-content').forEach(content => content.classList.remove('active'));
  document.getElementById(`tab-${tabId}`).classList.add('active');

  if (tabId === 'analytics') {
    resizeCanvas();
    fetchAnalyticsHistory();
  } else if (tabId === 'process-analytics') {
    initProcessHistorySelect();
    setTimeout(() => {
      drawProcessHistoryChart();
    }, 100);
  }
  
  setTimeout(() => {
    lucide.createIcons();
  }, 150);
}

// Fetch data from local API
async function pollData() {
  try {
    const promises = [
      fetch('/api/status').then(r => r.json()),
      fetch('/api/connections').then(r => r.json()),
      fetch('/api/logs').then(r => r.json())
    ];

    // If analytics tab is active and not in custom range, dynamically poll current timeline
    if (activeTab === 'analytics' && analyticsRange !== 'custom') {
      promises.push(fetch(`/api/analytics/history?range=${analyticsRange}`).then(r => r.json()));
    }

    const results = await Promise.all(promises);

    statusData = results[0];
    socketData = results[1];
    logData = results[2];

    if (results[3]) {
      analyticsPoints = results[3];
    }

    updateUI();
  } catch (err) {
    console.error("Failed to fetch statistics:", err);
    document.getElementById('statusText').innerText = "Disconnected";
    document.getElementById('statusText').parentElement.style.borderColor = "var(--danger-color)";
    document.getElementById('statusText').parentElement.style.color = "var(--danger-color)";
  }
}

// Update DOM elements with live data
function updateUI() {
  // Update version
  if (statusData.version) {
    document.getElementById('appVersion').innerText = `v${statusData.version}`;
  }

  // Update general status
  document.getElementById('statusText').innerText = "Active Guard";
  document.getElementById('statusText').parentElement.style.borderColor = "var(--success-color)";
  document.getElementById('statusText').parentElement.style.color = "var(--success-color)";

  // Update stats counters
  document.getElementById('statSocketsCount').innerText = socketData.length;
  
  const blockedCount = logData.filter(e => e.Action === 'Blocked' || e.State === 'Blocked').length;
  document.getElementById('statBlockedCount').innerText = blockedCount;

  // Dynamic blocked card duration text
  let durationText = 'Last 5m';
  if (analyticsRange === 'custom') {
    durationText = 'Custom Range';
  } else {
    const unitLabel = lookbackUnit === 'm' ? 'Min' : (lookbackUnit === 'h' ? 'Hours' : 'Days');
    durationText = `Last ${lookbackNum} ${unitLabel}`;
  }
  document.getElementById('statBlockedTitle').innerText = `Blocked (${durationText})`;

  // Format speed rates
  const rxStr = formatSpeed(statusData.rxSpeed);
  const txStr = formatSpeed(statusData.txSpeed);
  document.getElementById('statTrafficRx').innerText = `${rxStr} Down`;
  document.getElementById('statTrafficTx').innerText = `${txStr} Up`;

  document.getElementById('statFirewallMode').innerText = `${statusData.mode} Mode`;
  document.getElementById('statLockState').innerText = statusData.locked ? 'Locked' : 'Unlocked';

  // Panic Switch Class Toggle
  const panicBtn = document.getElementById('panicBtn');
  if (statusData.panicActive) {
    panicBtn.classList.add('active');
    panicBtn.innerHTML = '<i data-lucide="alert-triangle" style="width: 16px; height: 16px; display: inline-block; vertical-align: middle; margin-right: 4px;"></i> Global Panic Switch ACTIVE (Network Blocked)';
  } else {
    panicBtn.classList.remove('active');
    panicBtn.innerHTML = '<i data-lucide="zap" style="width: 16px; height: 16px; display: inline-block; vertical-align: middle; margin-right: 4px;"></i> Global Panic Switch';
  }

  // Redraw canvas if active
  if (activeTab === 'analytics') {
    drawChart();
    updateBandwidthList();
  } else if (activeTab === 'process-analytics') {
    drawProcessHistoryChart();
    updateProcessDetailStats();
  }

  // Populate grids
  renderSocketsTable();
  renderLogsTable();
  
  setTimeout(() => {
    lucide.createIcons();
  }, 100);
}

function formatSpeed(bytesPerSec) {
  const kb = bytesPerSec / 1024;
  if (kb > 1024) {
    return `${(kb / 1024).toFixed(1)} MiB/s`;
  }
  return `${kb.toFixed(1)} KiB/s`;
}

// Switch / Trigger Panic Switch API
async function togglePanicSwitch() {
  try {
    const res = await fetch('/api/action/panic', { method: 'POST' });
    const result = await res.json();
    statusData.panicActive = result.active;
    updateUI();
  } catch (err) {
    alert("Could not trigger panic switch.");
  }
}

// Render sockets grid
function renderSocketsTable() {
  const tbody = document.getElementById('socketsTableBody');
  let filtered = socketData.filter(s => {
    // Search filter
    const q = socketSearchQuery.toLowerCase();
    const matchesSearch = s.ProcessName.toLowerCase().includes(q) || 
                          s.Path.toLowerCase().includes(q) ||
                          s.RemoteAddress.toLowerCase().includes(q) ||
                          s.LocalAddress.toLowerCase().includes(q);

    // Tab category filter
    if (socketFilter === 'listening') return matchesSearch && s.State === 'Listening';
    if (socketFilter === 'active') return matchesSearch && s.State !== 'Listening';
    return matchesSearch;
  });

  // Sort
  filtered.sort((a, b) => {
    let valA = a[socketSortCol];
    let valB = b[socketSortCol];
    if (typeof valA === 'string') {
      return socketSortAsc ? valA.localeCompare(valB) : valB.localeCompare(valA);
    }
    return socketSortAsc ? valA - valB : valB - valA;
  });

  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" style="text-align: center; padding: 40px; color: var(--text-secondary);">No sockets matching search or filter query.</td></tr>`;
    return;
  }

  tbody.innerHTML = filtered.map(s => {
    const isListen = s.State === 'Listening';
    const badgeClass = isListen ? 'listening' : 'active';
    const safePath = s.Path.replace(/\\/g, '\\\\');
    
    return `
      <tr>
        <td>
          <div class="process-cell" style="display: flex; align-items: center; gap: 12px;">
            <i data-lucide="cpu" style="width: 16px; height: 16px; color: var(--text-secondary);"></i>
            <div class="process-info">
              <span class="process-name" style="display: flex; align-items: center; flex-wrap: wrap;">
                ${s.ProcessName}
                <span class="task-actions-inline">
                  <span class="inline-action-btn" title="Copy to Clipboard" onclick="openCopyModal('${s.ProcessName}', '${safePath}')">
                    <i data-lucide="copy" style="width: 12px; height: 12px;"></i>
                  </span>
                  <span class="inline-action-btn" title="Search Google" onclick="openSearchModal('${s.ProcessName}', '${safePath}')">
                    <i data-lucide="search" style="width: 12px; height: 12px;"></i>
                  </span>
                  <span class="inline-action-btn" title="Search on VirusTotal" onclick="searchVirusTotal('${safePath}', '${s.FileHash}')">
                    <i data-lucide="shield-alert" style="width: 12px; height: 12px;"></i>
                  </span>
                </span>
              </span>
              <span class="process-pid">PID: ${s.Pid}</span>
            </div>
          </div>
        </td>
        <td><span class="protocol-badge">${s.Protocol}</span></td>
        <td>${s.LocalAddress}:${s.LocalPort}</td>
        <td>${isListen ? '*' : `${s.RemoteAddress}:${s.RemotePort}`}</td>
        <td><span class="state-badge ${badgeClass}">${s.State}</span></td>
        <td>${s.Time}</td>
        <td>
          <button class="action-btn" title="VirusTotal Lookup" onclick="searchVirusTotal('${safePath}', '${s.FileHash}')">
            <i data-lucide="shield-alert" style="width: 14px; height: 14px; color: white;"></i>
          </button>
          <button class="action-btn terminate" title="Terminate Process" onclick="terminateProcess(${s.Pid})">
            <i data-lucide="trash-2" style="width: 14px; height: 14px; color: white;"></i>
          </button>
        </td>
      </tr>
    `;
  }).join('');
}

// Render logs table
function renderLogsTable() {
  const tbody = document.getElementById('logsTableBody');
  let filtered = logData.filter(l => {
    const q = logSearchQuery.toLowerCase();
    const matchesSearch = l.ProcessName.toLowerCase().includes(q) || 
                          l.Path.toLowerCase().includes(q) ||
                          l.RemoteAddress.toLowerCase().includes(q);

    if (logFilter === 'blocked') return matchesSearch && l.Action === 'Blocked';
    if (logFilter === 'allowed') return matchesSearch && l.Action === 'Allowed';
    return matchesSearch;
  });

  // Sort
  filtered.sort((a, b) => {
    let valA = a[logSortCol];
    let valB = b[logSortCol];
    if (typeof valA === 'string') {
      return logSortAsc ? valA.localeCompare(valB) : valB.localeCompare(valA);
    }
    return logSortAsc ? valA - valB : valB - valA;
  });

  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="8" style="text-align: center; padding: 40px; color: var(--text-secondary);">No recent events found.</td></tr>`;
    return;
  }

  tbody.innerHTML = filtered.map(l => {
    const isBlocked = l.Action === 'Blocked';
    const badgeClass = isBlocked ? 'blocked' : 'active';
    const safePath = l.Path.replace(/\\/g, '\\\\');
    
    return `
      <tr>
        <td>${l.Time}</td>
        <td>
          <div class="process-cell" style="display: flex; align-items: center; gap: 12px;">
            <i data-lucide="shield" style="width: 16px; height: 16px; color: var(--text-secondary);"></i>
            <div class="process-info">
              <span class="process-name" style="display: flex; align-items: center; flex-wrap: wrap;">
                ${l.ProcessName}
                <span class="task-actions-inline">
                  <span class="inline-action-btn" title="Copy to Clipboard" onclick="openCopyModal('${l.ProcessName}', '${safePath}')">
                    <i data-lucide="copy" style="width: 12px; height: 12px;"></i>
                  </span>
                  <span class="inline-action-btn" title="Search Google" onclick="openSearchModal('${l.ProcessName}', '${safePath}')">
                    <i data-lucide="search" style="width: 12px; height: 12px;"></i>
                  </span>
                  <span class="inline-action-btn" title="Search on VirusTotal" onclick="searchVirusTotal('${safePath}', '${l.FileHash}')">
                    <i data-lucide="shield-alert" style="width: 12px; height: 12px;"></i>
                  </span>
                </span>
              </span>
              <span class="process-pid">PID: ${l.Pid}</span>
            </div>
          </div>
        </td>
        <td><span class="protocol-badge">${l.Protocol}</span></td>
        <td>${l.Direction}</td>
        <td>${l.LocalAddress}:${l.LocalPort}</td>
        <td>${l.RemoteAddress}:${l.RemotePort}</td>
        <td><span class="state-badge ${badgeClass}">${l.Action}</span></td>
        <td>
          <button class="action-btn" title="Quick Whitelist App" onclick="quickWhitelist('${safePath}')">
            <i data-lucide="plus-circle" style="width: 14px; height: 14px; color: white;"></i>
          </button>
        </td>
      </tr>
    `;
  }).join('');
}

// Action Handlers
async function quickWhitelist(path) {
  if (confirm(`Do you want to whitelist and allow network access for ${path}?`)) {
    try {
      const res = await fetch(`/api/action/whitelist?path=${encodeURIComponent(path)}`, { method: 'POST' });
      if (res.ok) {
        alert("Whitelisted successfully!");
        pollData();
      } else {
        alert("Whitelisting failed.");
      }
    } catch (err) {
      alert("Error whitelisting.");
    }
  }
}

async function terminateProcess(pid) {
  if (confirm(`Are you absolutely sure you want to terminate process ID ${pid}?`)) {
    try {
      const res = await fetch(`/api/action/terminate?pid=${pid}`, { method: 'POST' });
      if (res.ok) {
        alert("Process terminated successfully!");
        pollData();
      } else {
        alert("Could not terminate process.");
      }
    } catch (err) {
      alert("Error terminating process.");
    }
  }
}

function virusTotalLookup(path) {
  // Triggers search on VT via local API action or launches search window
  window.open(`https://www.virustotal.com/gui/search/${encodeURIComponent(path.split('\\').pop())}`);
}

// Table Filters & Sorters
function filterSockets() {
  socketSearchQuery = document.getElementById('socketSearch').value;
  renderSocketsTable();
}

function filterSocketsCategory(category, element) {
  socketFilter = category;
  document.querySelectorAll('#tab-sockets .filter-btn').forEach(btn => btn.classList.remove('active'));
  element.classList.add('active');
  renderSocketsTable();
}

function filterLogs() {
  logSearchQuery = document.getElementById('logSearch').value;
  renderLogsTable();
}

function filterLogsCategory(category, element) {
  logFilter = category;
  document.querySelectorAll('#tab-logs .filter-btn').forEach(btn => btn.classList.remove('active'));
  element.classList.add('active');
  renderLogsTable();
}

function sortTable(col) {
  if (socketSortCol === col) {
    socketSortAsc = !socketSortAsc;
  } else {
    socketSortCol = col;
    socketSortAsc = true;
  }
  renderSocketsTable();
}

function sortLogs(col) {
  if (logSortCol === col) {
    logSortAsc = !logSortAsc;
  } else {
    logSortCol = col;
    logSortAsc = true;
  }
  renderLogsTable();
}

/* Real-Time HTML5 Canvas Chart Rendering */
let chartCanvas, ctx;
let activeHoverIndex = -1;

function initChart() {
  chartCanvas = document.getElementById('bandwidthChart');
  ctx = chartCanvas.getContext('2d');
  
  chartCanvas.addEventListener('mousemove', handleChartHover);
  chartCanvas.addEventListener('mouseleave', handleChartLeave);
  chartCanvas.addEventListener('click', handleChartClick);
  
  window.addEventListener('resize', resizeCanvas);
  resizeCanvas();
}

function handleChartHover(e) {
  if (!analyticsPoints || analyticsPoints.length === 0) return;
  const rect = chartCanvas.getBoundingClientRect();
  const mouseX = e.clientX - rect.left;
  
  const w = chartCanvas.width;
  const index = Math.round((mouseX / w) * (analyticsPoints.length - 1));
  if (index >= 0 && index < analyticsPoints.length) {
    activeHoverIndex = index;
    const point = analyticsPoints[index];
    showChartTooltip(e.clientX, e.clientY, point);
    drawChart();
  }
}

function handleChartClick(e) {
  if (!analyticsPoints || analyticsPoints.length === 0) return;
  const rect = chartCanvas.getBoundingClientRect();
  const mouseX = e.clientX - rect.left;
  
  const w = chartCanvas.width;
  const index = Math.round((mouseX / w) * (analyticsPoints.length - 1));
  if (index >= 0 && index < analyticsPoints.length) {
    const point = analyticsPoints[index];
    openClickDetailModal(point);
  }
}

function openClickDetailModal(point) {
  const d = new Date(point.Time);
  const timeStr = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  const dateStr = d.toLocaleDateString();
  
  document.getElementById('clickModalTimestamp').innerText = `${dateStr} ${timeStr}`;
  document.getElementById('clickModalRx').innerText = formatSpeed(point.Rx);
  document.getElementById('clickModalTx').innerText = formatSpeed(point.Tx);
  
  const tbody = document.getElementById('clickModalTableBody');
  const rawTasks = point.PeakTask || 'System Service (Idle)';
  
  if (rawTasks === 'Idle') {
    tbody.innerHTML = `<tr><td colspan="3" style="text-align: center; color: var(--text-secondary); padding: 20px;">System was idle at this point.</td></tr>`;
  } else {
    const tasks = rawTasks.split(';');
    tbody.innerHTML = tasks.map((t, idx) => {
      const parts = t.split(' (');
      const name = parts[0];
      const speed = parts[1] ? parts[1].replace(')', '') : '0.0 KiB/s';
      
      // Look up real path and hash from live/log data
      let resolvedPath = '';
      let resolvedHash = '';
      
      const socketMatch = socketData.find(s => s.ProcessName.toLowerCase() === name.toLowerCase());
      if (socketMatch) {
        resolvedPath = socketMatch.Path;
        resolvedHash = socketMatch.FileHash;
      } else {
        const logMatch = logData.find(l => l.ProcessName.toLowerCase() === name.toLowerCase());
        if (logMatch) {
          resolvedPath = logMatch.Path;
          resolvedHash = logMatch.FileHash;
        }
      }
      
      if (!resolvedPath) {
        resolvedPath = `C:\\Windows\\System32\\${name}.exe`;
      }
      const safePath = resolvedPath.replace(/\\/g, '\\\\');
      
      return `
        <tr>
          <td style="padding: 10px 16px; font-weight: 600; color: white;">${idx + 1}. ${name}</td>
          <td style="padding: 10px 16px; text-align: right; color: #ffcc00; font-weight: 600;">${speed}</td>
          <td style="padding: 10px 16px; text-align: center;">
            <span class="inline-action-btn" title="Copy to Clipboard" onclick="openCopyModal('${name}', '${safePath}')" style="margin: 0 4px; display: inline-block;">
              <i data-lucide="copy" style="width: 12px; height: 12px;"></i>
            </span>
            <span class="inline-action-btn" title="Search Google" onclick="openSearchModal('${name}', '${safePath}')" style="margin: 0 4px; display: inline-block;">
              <i data-lucide="search" style="width: 12px; height: 12px;"></i>
            </span>
            <span class="inline-action-btn" title="Search on VirusTotal" onclick="searchVirusTotal('${safePath}', '${resolvedHash}')" style="margin: 0 4px; display: inline-block;">
              <i data-lucide="shield-alert" style="width: 12px; height: 12px;"></i>
            </span>
          </td>
        </tr>
      `;
    }).join('');
  }
  
  document.getElementById('chartClickDetailModal').style.display = 'flex';
  
  setTimeout(() => {
    lucide.createIcons();
  }, 100);
}

function handleChartLeave() {
  activeHoverIndex = -1;
  const tooltip = document.getElementById('chartTooltip');
  if (tooltip) {
    tooltip.style.opacity = '0';
  }
  drawChart();
}

function showChartTooltip(clientX, clientY, point) {
  let tooltip = document.getElementById('chartTooltip');
  if (!tooltip) {
    tooltip = document.createElement('div');
    tooltip.id = 'chartTooltip';
    document.body.appendChild(tooltip);
  }
  
  // Format Date and Time
  const d = new Date(point.Time);
  const timeStr = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  const dateStr = d.toLocaleDateString();
  
  const detailedChecked = document.getElementById('detailedTooltipCheck')?.checked;
  const rawTasks = point.PeakTask || 'System Service (Idle)';
  
  let taskHtml = '';
  if (detailedChecked && rawTasks.includes(';')) {
    const tasks = rawTasks.split(';');
    taskHtml = tasks.map((t, idx) => {
      const parts = t.split(' (');
      const name = parts[0];
      const speed = parts[1] ? parts[1].replace(')', '') : '0.0 KiB/s';
      return `
        <div style="display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-top: 5px; font-size: 11px;">
          <span style="color: white; font-weight: 500; display: flex; align-items: center; gap: 4px;">
            <i data-lucide="chevron-right" style="width: 10px; height: 10px; color: var(--accent-color);"></i> ${idx + 1}. ${name}
          </span>
          <span style="color: #ffcc00; font-weight: 600;">${speed}</span>
        </div>
      `;
    }).join('');
  } else {
    // Show only the single heaviest peak task
    const topTask = rawTasks.split(';')[0];
    taskHtml = `
      <div style="color: #ffcc00; font-weight: 600; font-size: 12px; margin-top: 2px; display: flex; align-items: center; gap: 4px;">
        <i data-lucide="zap" style="width: 12px; height: 12px; color: #ffcc00;"></i> <span>${topTask}</span>
      </div>
    `;
  }
  
  tooltip.innerHTML = `
    <div style="font-weight: 600; color: var(--text-secondary); font-size: 11px; margin-bottom: 4px;">${dateStr} ${timeStr}</div>
    <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 2px;">
      <span style="color: var(--success-color); font-size: 10px;">●</span>
      <span style="font-size: 12px;">Down: <strong style="color: white;">${formatSpeed(point.Rx)}</strong></span>
    </div>
    <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 6px;">
      <span style="color: var(--accent-color); font-size: 10px;">●</span>
      <span style="font-size: 12px;">Up: <strong style="color: white;">${formatSpeed(point.Tx)}</strong></span>
    </div>
    <div style="margin-top: 4px; padding-top: 6px; border-top: 1px solid rgba(255,255,255,0.08);">
      <div style="color: var(--text-secondary); font-size: 9px; text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600;">
        ${detailedChecked ? 'Parsed Task Breakdown' : 'Heavy Peak Task'}
      </div>
      ${taskHtml}
    </div>
  `;
  
  tooltip.style.left = `${clientX + window.scrollX}px`;
  tooltip.style.top = `${clientY + window.scrollY}px`;
  tooltip.style.opacity = '1';
}

function resizeCanvas() {
  if (!chartCanvas) return;
  const rect = chartCanvas.parentElement.getBoundingClientRect();
  chartCanvas.width = rect.width;
  chartCanvas.height = 300;
  drawChart();
  drawProcessHistoryChart();
}

function drawChart() {
  if (!ctx) return;
  
  const w = chartCanvas.width;
  const h = chartCanvas.height;
  ctx.clearRect(0, 0, w, h);

  const points = analyticsPoints;
  if (points.length === 0) {
    ctx.font = '14px Outfit';
    ctx.fillStyle = 'var(--text-secondary)';
    ctx.textAlign = 'center';
    ctx.fillText("No historical network data recorded in this range.", w / 2, h / 2);
    return;
  }

  // Draw background grid lines
  ctx.strokeStyle = 'rgba(255,255,255,0.03)';
  ctx.lineWidth = 1;
  for (let i = 1; i < 5; i++) {
    const y = (h / 5) * i;
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(w, y);
    ctx.stroke();
  }

  // Find max value in history to scale (convert bytes to KB)
  const rxValues = points.map(p => p.Rx / 1024);
  const txValues = points.map(p => p.Tx / 1024);
  const maxVal = Math.max(...rxValues, ...txValues, 10); // Minimum scale of 10 KB/s

  // Helper to get coordinates
  function getX(index) {
    if (points.length <= 1) return w / 2;
    return (w / (points.length - 1)) * index;
  }

  function getY(value) {
    return h - (h - 60) * (value / maxVal) - 30;
  }

  // Draw Rx (Download) Area & Line
  drawPath(rxValues, 'rgba(0, 255, 204, 0.15)', 'rgba(0, 255, 204, 1)', 2);

  // Draw Tx (Upload) Area & Line
  drawPath(txValues, 'rgba(138, 43, 226, 0.15)', 'rgba(138, 43, 226, 1)', 2);

  function drawPath(history, fillGradient, strokeColor, lineWidth) {
    if (points.length === 0) return;
    ctx.beginPath();
    ctx.moveTo(getX(0), getY(history[0]));

    for (let i = 1; i < points.length; i++) {
      ctx.lineTo(getX(i), getY(history[i]));
    }

    // Line stroke
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = lineWidth;
    ctx.shadowBlur = 10;
    ctx.shadowColor = strokeColor;
    ctx.stroke();
    ctx.shadowBlur = 0; // reset

    // Fill area
    ctx.lineTo(getX(points.length - 1), h);
    ctx.lineTo(getX(0), h);
    ctx.closePath();
    ctx.fillStyle = fillGradient;
    ctx.fill();
  }

  // Draw Vertical Highlight Line and Dot if Hovered
  if (activeHoverIndex >= 0 && activeHoverIndex < points.length) {
    const hx = getX(activeHoverIndex);
    
    // Draw vertical dashed line
    ctx.beginPath();
    ctx.setLineDash([5, 5]);
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
    ctx.moveTo(hx, 0);
    ctx.lineTo(hx, h);
    ctx.stroke();
    ctx.setLineDash([]); // Reset line dash
    
    // Draw active dots for both lines
    const activeRxY = getY(rxValues[activeHoverIndex]);
    const activeTxY = getY(txValues[activeHoverIndex]);
    
    // Rx glowing dot
    ctx.beginPath();
    ctx.arc(hx, activeRxY, 6, 0, 2 * Math.PI);
    ctx.fillStyle = '#00ffcc';
    ctx.shadowBlur = 15;
    ctx.shadowColor = '#00ffcc';
    ctx.fill();
    ctx.lineWidth = 2;
    ctx.strokeStyle = '#ffffff';
    ctx.stroke();
    
    // Tx glowing dot
    ctx.beginPath();
    ctx.arc(hx, activeTxY, 6, 0, 2 * Math.PI);
    ctx.fillStyle = '#8a2be2';
    ctx.shadowBlur = 15;
    ctx.shadowColor = '#8a2be2';
    ctx.fill();
    ctx.stroke();
    
    ctx.shadowBlur = 0; // Reset glow
  }

  // Draw legend labels on chart
  ctx.font = '12px Outfit';
  ctx.textAlign = 'left';
  ctx.fillStyle = '#00ffcc';
  ctx.fillText(`Down: ${formatSpeed(statusData.rxSpeed)}`, 20, 30);
  ctx.fillStyle = '#8a2be2';
  ctx.fillText(`Up: ${formatSpeed(statusData.txSpeed)}`, 20, 50);

  // Draw Dynamic Timeline X-Axis Labels at Bottom (Start, Mid, End)
  if (points.length >= 2) {
    ctx.font = '10px Outfit';
    ctx.fillStyle = 'var(--text-secondary)';
    
    function formatTimelineLabel(timeStr) {
      const d = new Date(timeStr);
      const timePart = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      const datePart = d.toLocaleDateString([], { month: 'short', day: 'numeric' });
      return `${datePart} ${timePart}`;
    }

    // Start label
    ctx.textAlign = 'left';
    ctx.fillText(formatTimelineLabel(points[0].Time), 20, h - 10);

    // End label
    ctx.textAlign = 'right';
    ctx.fillText(formatTimelineLabel(points[points.length - 1].Time), w - 20, h - 10);

    // Middle label
    if (points.length >= 3) {
      ctx.textAlign = 'center';
      const midIdx = Math.floor(points.length / 2);
      ctx.fillText(formatTimelineLabel(points[midIdx].Time), w / 2, h - 10);
    }
  }
}

// Fetch historical analytics data
async function fetchAnalyticsHistory() {
  try {
    let url = `/api/analytics/history?range=${analyticsRange}`;
    if (analyticsRange === 'custom') {
      const dateStart = document.getElementById('customDateStart').value;
      const dateEnd = document.getElementById('customDateEnd').value;
      const timeStart = document.getElementById('customTimeStart').value;
      const timeEnd = document.getElementById('customTimeEnd').value;
      
      const start = `${dateStart}T${timeStart}:00`;
      const end = `${dateEnd}T${timeEnd}:59`;
      
      url += `&start=${encodeURIComponent(start)}&end=${encodeURIComponent(end)}`;
    }

    const res = await fetch(url);
    analyticsPoints = await res.json();
    drawChart();
  } catch (err) {
    console.error("Could not fetch analytics history:", err);
  }
}

// Dynamic Lookback Selections
let lookbackNum = 5;
let lookbackUnit = 'm';

function selectLookbackNum(num, btn) {
  lookbackNum = parseInt(num);
  document.getElementById('customLookbackNum').value = num;
  
  // Toggle active button class
  document.querySelectorAll('#quickNumGroup .filter-btn').forEach(b => b.classList.remove('active'));
  if (btn) btn.classList.add('active');
  
  triggerLookbackFetch();
}

function onCustomLookbackNumChange() {
  const inputVal = parseInt(document.getElementById('customLookbackNum').value);
  if (isNaN(inputVal) || inputVal <= 0) return;
  lookbackNum = inputVal;
  
  // Highlight lookback button matching custom number if exists, otherwise remove active highlights
  document.querySelectorAll('#quickNumGroup .filter-btn').forEach(btn => {
    if (parseInt(btn.innerText) === inputVal) btn.classList.add('active');
    else btn.classList.remove('active');
  });
  
  triggerLookbackFetch();
}

function selectLookbackUnit(unit, btn) {
  lookbackUnit = unit;
  
  // Toggle active button class
  document.querySelectorAll('#quickUnitGroup .filter-btn').forEach(b => b.classList.remove('active'));
  if (btn) btn.classList.add('active');
  
  triggerLookbackFetch();
}

function triggerLookbackFetch() {
  analyticsRange = `${lookbackNum}${lookbackUnit}`;
  
  // Hide custom range picker when toggling standard lookback durations
  document.getElementById('customRangePicker').style.display = 'none';
  document.getElementById('customRangeToggleBtn').classList.remove('active');
  
  fetchAnalyticsHistory();
}

function toggleCustomRangePicker() {
  const picker = document.getElementById('customRangePicker');
  const toggleBtn = document.getElementById('customRangeToggleBtn');
  
  if (picker.style.display === 'none' || picker.style.display === '') {
    picker.style.display = 'flex';
    toggleBtn.classList.add('active');
    analyticsRange = 'custom';
  } else {
    picker.style.display = 'none';
    toggleBtn.classList.remove('active');
    // Revert back to lookback window
    triggerLookbackFetch();
  }
}

// Handler for custom date range range picker
function applyCustomAnalyticsRange() {
  analyticsRange = 'custom';
  fetchAnalyticsHistory();
}

// Utility Action Helpers
let currentCopyData = { name: '', path: '' };
let currentSearchData = { name: '', path: '' };

function openCopyModal(name, path) {
  currentCopyData = { name, path };
  document.getElementById('copyModalNamePreview').innerText = name;
  document.getElementById('copyModalPathPreview').innerText = path || name;
  document.getElementById('copyModalCombinedPreview').innerText = path ? `${name} - ${path}` : name;
  document.getElementById('copyOptionsModal').style.display = 'flex';
}

function confirmCopyToClipboard() {
  const selectedOption = document.querySelector('input[name="copyField"]:checked').value;
  let copiedText = '';
  
  if (selectedOption === 'name') {
    copiedText = currentCopyData.name;
  } else if (selectedOption === 'path') {
    copiedText = currentCopyData.path;
  } else {
    copiedText = currentCopyData.path ? `${currentCopyData.name} - ${currentCopyData.path}` : currentCopyData.name;
  }
  
  navigator.clipboard.writeText(copiedText).then(() => {
    showToast("Copied selection to clipboard!");
    closeModal('copyOptionsModal');
  }).catch(() => {
    alert("Failed to copy data.");
  });
}

function openSearchModal(name, path) {
  currentSearchData = { name, path };
  const fileName = path ? path.split('\\').pop().split('.').shift() : name;
  document.getElementById('searchModalNamePreview').innerText = name;
  document.getElementById('searchModalFilePreview').innerText = fileName;
  
  // Set prompt query checkbox to checked by default
  const promptCheck = document.getElementById('searchPromptCheck');
  if (promptCheck) promptCheck.checked = true;
  
  // Populate the editable input with process name by default
  toggleSearchOptionSelection('name');
  
  document.getElementById('searchOptionsModal').style.display = 'flex';
}

function toggleSearchOptionSelection(field) {
  let rawValue = '';
  if (field === 'name') {
    rawValue = currentSearchData.name;
  } else {
    const path = currentSearchData.path;
    rawValue = path ? path.split('\\').pop().split('.').shift() : currentSearchData.name;
  }
  
  const queryInput = document.getElementById('searchPromptQueryInput');
  if (queryInput) {
    queryInput.value = `is ${rawValue} safe legitimate or malware virus`;
  }
}

function toggleSearchPromptEdit() {
  const promptCheck = document.getElementById('searchPromptCheck');
  const queryInput = document.getElementById('searchPromptQueryInput');
  if (promptCheck && queryInput) {
    queryInput.disabled = !promptCheck.checked;
    queryInput.style.opacity = promptCheck.checked ? '1' : '0.5';
  }
}

function confirmSearchGoogle() {
  const promptCheck = document.getElementById('searchPromptCheck');
  const queryInput = document.getElementById('searchPromptQueryInput');
  
  let finalQuery = '';
  if (promptCheck && promptCheck.checked && queryInput && queryInput.value.trim().length > 0) {
    finalQuery = queryInput.value;
  } else {
    const selectedOption = document.querySelector('input[name="searchField"]:checked').value;
    let rawValue = '';
    if (selectedOption === 'name') {
      rawValue = currentSearchData.name;
    } else {
      const path = currentSearchData.path;
      rawValue = path ? path.split('\\').pop().split('.').shift() : currentSearchData.name;
    }
    finalQuery = `is ${rawValue} safe legitimate or malware virus`;
  }
  
  window.open(`https://www.google.com/search?q=${encodeURIComponent(finalQuery)}`, '_blank');
  closeModal('searchOptionsModal');
}

function searchVirusTotal(path, fileHash) {
  if (fileHash && fileHash.length > 0 && fileHash !== "null" && fileHash !== "undefined") {
    window.open(`https://www.virustotal.com/gui/search/${fileHash}`, '_blank');
    showToast("Searching VirusTotal with C# Computed SHA1 Hash!");
  } else {
    const filename = path ? path.split('\\').pop() : path;
    window.open(`https://www.virustotal.com/gui/search/${encodeURIComponent(filename)}`, '_blank');
    showToast("Searching VirusTotal with process filename...");
  }
}

function closeModal(id) {
  document.getElementById(id).style.display = 'none';
}

function showToast(msg) {
  let toast = document.getElementById('appToast');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'appToast';
    document.body.appendChild(toast);
  }
  toast.innerText = msg;
  toast.className = 'show';
  setTimeout(() => { toast.className = ''; }, 2500);
}

// Process Analytics Tab Helper Logic
let selectedProcessHistoryTarget = '';

function initProcessHistorySelect() {
  const select = document.getElementById('processHistorySelect');
  if (!select) return;
  
  // Extract unique background process names from history logs
  const processes = new Set();
  analyticsPoints.forEach(p => {
    if (p.PeakTask && p.PeakTask !== 'Idle') {
      p.PeakTask.split(';').forEach(t => {
        const name = t.split(' (')[0].trim();
        if (name && name !== 'System Service') {
          processes.add(name);
        }
      });
    }
  });
  
  // Fallback to active connections if history is young
  socketData.forEach(s => {
    if (s.ProcessName && s.ProcessName !== 'System / Services') {
      processes.add(s.ProcessName);
    }
  });
  
  const sorted = Array.from(processes).sort();
  if (sorted.length === 0) {
    select.innerHTML = '<option value="">No active network tasks found</option>';
    return;
  }
  
  const oldVal = select.value || sorted[0];
  select.innerHTML = sorted.map(p => `<option value="${p}" ${p === oldVal ? 'selected' : ''}>${p}</option>`).join('');
  selectedProcessHistoryTarget = select.value;
  updateProcessDetailStats();
}

function changeProcessHistoryTarget(val) {
  selectedProcessHistoryTarget = val;
  updateProcessDetailStats();
  drawProcessHistoryChart();
}

function updateProcessDetailStats() {
  if (!selectedProcessHistoryTarget) return;
  
  // 1. Active Connections
  const conns = socketData.filter(s => s.ProcessName === selectedProcessHistoryTarget).length;
  document.getElementById('procDetailActiveConns').innerText = `${conns} conns`;
  
  // 2. Peak Speed in History
  let peakBytes = 0;
  analyticsPoints.forEach(p => {
    if (p.PeakTask && p.PeakTask.includes(selectedProcessHistoryTarget)) {
      p.PeakTask.split(';').forEach(t => {
        if (t.startsWith(selectedProcessHistoryTarget)) {
          const speedPart = t.split('(')[1]?.replace(')', '').trim();
          if (speedPart) {
            let bytes = parseFloat(speedPart);
            if (speedPart.includes('MiB/s')) bytes *= 1024 * 1024;
            else if (speedPart.includes('KiB/s')) bytes *= 1024;
            if (bytes > peakBytes) peakBytes = bytes;
          }
        }
      });
    }
  });
  document.getElementById('procDetailPeakSpeed').innerText = formatSpeed(peakBytes);
  
  // 3. Events Logged
  const events = logData.filter(l => l.ProcessName === selectedProcessHistoryTarget).length;
  document.getElementById('procDetailAlerts').innerText = `${events} occurrences`;
}

function drawProcessHistoryChart() {
  const canvas = document.getElementById('processBandwidthChart');
  if (!canvas) return;
  
  const pCtx = canvas.getContext('2d');
  const w = canvas.width = canvas.parentElement.getBoundingClientRect().width;
  const h = canvas.height = 300;
  pCtx.clearRect(0, 0, w, h);
  
  const points = analyticsPoints;
  if (!selectedProcessHistoryTarget || points.length === 0) {
    pCtx.font = '14px Outfit';
    pCtx.fillStyle = 'var(--text-secondary)';
    pCtx.textAlign = 'center';
    pCtx.fillText("Select an application from the dropdown to view its history.", w / 2, h / 2);
    return;
  }
  
  // Parse speeds at each timestamp
  const values = points.map(p => {
    let speedBytes = 0;
    if (p.PeakTask && p.PeakTask.includes(selectedProcessHistoryTarget)) {
      p.PeakTask.split(';').forEach(t => {
        if (t.startsWith(selectedProcessHistoryTarget)) {
          const speedPart = t.split('(')[1]?.replace(')', '').trim();
          if (speedPart) {
            let bytes = parseFloat(speedPart);
            if (speedPart.includes('MiB/s')) bytes *= 1024 * 1024;
            else if (speedPart.includes('KiB/s')) bytes *= 1024;
            speedBytes = bytes;
          }
        }
      });
    }
    return speedBytes / 1024; // KB/s
  });
  
  const maxVal = Math.max(...values, 10);
  
  // Draw background grid lines
  pCtx.strokeStyle = 'rgba(255,255,255,0.03)';
  pCtx.lineWidth = 1;
  for (let i = 1; i < 5; i++) {
    const y = (h / 5) * i;
    pCtx.beginPath();
    pCtx.moveTo(0, y);
    pCtx.lineTo(w, y);
    pCtx.stroke();
  }
  
  function getX(index) {
    if (points.length <= 1) return w / 2;
    return (w / (points.length - 1)) * index;
  }
  
  function getY(value) {
    return h - (h - 60) * (value / maxVal) - 30;
  }
  
  // Draw magenta process history line
  pCtx.beginPath();
  pCtx.moveTo(getX(0), getY(values[0]));
  for (let i = 1; i < points.length; i++) {
    pCtx.lineTo(getX(i), getY(values[i]));
  }
  
  pCtx.strokeStyle = 'var(--magenta-color)';
  pCtx.lineWidth = 2.5;
  pCtx.shadowBlur = 12;
  pCtx.shadowColor = 'var(--magenta-glow)';
  pCtx.stroke();
  pCtx.shadowBlur = 0;
  
  pCtx.lineTo(getX(points.length - 1), h);
  pCtx.lineTo(getX(0), h);
  pCtx.closePath();
  pCtx.fillStyle = 'rgba(255, 0, 127, 0.12)';
  pCtx.fill();
  
  // Draw bottom dynamic X-Axis labels
  if (points.length >= 2) {
    pCtx.font = '10px Outfit';
    pCtx.fillStyle = 'var(--text-secondary)';
    
    function formatProcessLabel(timeStr) {
      const d = new Date(timeStr);
      return `${d.toLocaleDateString([], {month:'short', day:'numeric'})} ${d.toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'})}`;
    }
    
    pCtx.textAlign = 'left';
    pCtx.fillText(formatProcessLabel(points[0].Time), 20, h - 10);
    pCtx.textAlign = 'right';
    pCtx.fillText(formatProcessLabel(points[points.length - 1].Time), w - 20, h - 10);
  }
  
  // Top speed indicator legend label
  pCtx.font = '13px Outfit';
  pCtx.textAlign = 'left';
  pCtx.fillStyle = 'var(--magenta-color)';
  pCtx.fillText(`Selected App: ${selectedProcessHistoryTarget} (Peak: ${formatSpeed(Math.max(...values) * 1024)})`, 20, 30);
}

// Generate Bandwidth by Process indicators
function updateBandwidthList() {
  const container = document.getElementById('bandwidthList');
  
  // Compile unique active socket apps and sort by simulated throughput / connection counts
  const apps = {};
  socketData.forEach(s => {
    if (!apps[s.ProcessName]) {
      apps[s.ProcessName] = { name: s.ProcessName, count: 0 };
    }
    apps[s.ProcessName].count++;
  });

  const sortedApps = Object.values(apps).sort((a, b) => b.count - a.count);

  if (sortedApps.length === 0) {
    container.innerHTML = `<div style="text-align: center; color: var(--text-secondary); padding: 40px;">No active socket connections.</div>`;
    return;
  }

  const maxCount = Math.max(...sortedApps.map(a => a.count));

  container.innerHTML = sortedApps.slice(0, 5).map(a => {
    const widthPercentage = (a.count / maxCount) * 100;
    
    // Look up real path and hash from live/log data
    let resolvedPath = '';
    let resolvedHash = '';
    
    const socketMatch = socketData.find(s => s.ProcessName.toLowerCase() === a.name.toLowerCase());
    if (socketMatch) {
      resolvedPath = socketMatch.Path;
      resolvedHash = socketMatch.FileHash;
    } else {
      const logMatch = logData.find(l => l.ProcessName.toLowerCase() === a.name.toLowerCase());
      if (logMatch) {
        resolvedPath = logMatch.Path;
        resolvedHash = logMatch.FileHash;
      }
    }
    
    if (!resolvedPath) {
      resolvedPath = a.name;
    }
    const safePath = resolvedPath.replace(/\\/g, '\\\\');

    return `
      <div class="bandwidth-item">
        <div class="bandwidth-header" style="align-items: center;">
          <span class="bandwidth-app" style="display: flex; align-items: center; gap: 8px;">
            ${a.name}
            <span class="task-actions-inline">
              <span class="inline-action-btn" title="Copy to Clipboard" onclick="openCopyModal('${a.name}', '${safePath}')">
                <i data-lucide="copy" style="width: 12px; height: 12px;"></i>
              </span>
              <span class="inline-action-btn" title="Search Google" onclick="openSearchModal('${a.name}', '${safePath}')">
                <i data-lucide="search" style="width: 12px; height: 12px;"></i>
              </span>
              <span class="inline-action-btn" title="Search on VirusTotal" onclick="searchVirusTotal('${safePath}', '${resolvedHash}')">
                <i data-lucide="shield-alert" style="width: 12px; height: 12px;"></i>
              </span>
            </span>
          </span>
          <span style="color: var(--text-secondary);">${a.count} active connections</span>
        </div>
        <div class="bandwidth-track">
          <div class="bandwidth-bar" style="width: ${widthPercentage}%;"></div>
        </div>
      </div>
    `;
  }).join('');
}
