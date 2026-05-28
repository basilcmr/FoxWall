/* FoxWall Security Monitor Dashboard Logic */

let activeTab = 'sockets';
let socketData = [];
let logData = [];
let statusData = { mode: 'Normal', locked: false, rxSpeed: 0, txSpeed: 0, panicActive: false };
let socketFilter = 'all';
let socketSearchQuery = '';
let logFilter = 'all';
let logSearchQuery = '';

// Chart history
const maxHistory = 60;
const rxHistory = new Array(maxHistory).fill(0);
const txHistory = new Array(maxHistory).fill(0);

// Sorting states
let socketSortCol = 'time';
let socketSortAsc = false;
let logSortCol = 'time';
let logSortAsc = false;

// Initialize
window.onload = () => {
  initChart();
  pollData();
  setInterval(pollData, 1500);
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
  }
}

// Fetch data from local API
async function pollData() {
  try {
    const [statusRes, connectionsRes, logsRes] = await Promise.all([
      fetch('/api/status').then(r => r.json()),
      fetch('/api/connections').then(r => r.json()),
      fetch('/api/logs').then(r => r.json())
    ]);

    statusData = statusRes;
    socketData = connectionsRes;
    logData = logsRes;

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
  // Update general status
  document.getElementById('statusText').innerText = "Active Guard";
  document.getElementById('statusText').parentElement.style.borderColor = "var(--success-color)";
  document.getElementById('statusText').parentElement.style.color = "var(--success-color)";

  // Update stats counters
  document.getElementById('statSocketsCount').innerText = socketData.length;
  
  const blockedCount = logData.filter(e => e.Action === 'Blocked' || e.State === 'Blocked').length;
  document.getElementById('statBlockedCount').innerText = blockedCount;

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
    panicBtn.innerHTML = '⚠️ Panic Switch ACTIVE (Network Blocked)';
  } else {
    panicBtn.classList.remove('active');
    panicBtn.innerHTML = '⚠️ Global Panic Switch';
  }

  // Update chart data
  rxHistory.push(statusData.rxSpeed / 1024); // KB/s
  rxHistory.shift();
  txHistory.push(statusData.txSpeed / 1024); // KB/s
  txHistory.shift();

  // Redraw canvas if active
  if (activeTab === 'analytics') {
    drawChart();
    updateBandwidthList();
  }

  // Populate grids
  renderSocketsTable();
  renderLogsTable();
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
    
    return `
      <tr>
        <td>
          <div class="process-cell">
            <span style="font-size: 18px;">📄</span>
            <div class="process-info">
              <span class="process-name">${s.ProcessName}</span>
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
          <button class="action-btn" title="VirusTotal Lookup" onclick="virusTotalLookup('${s.Path}')">🔍</button>
          <button class="action-btn terminate" title="Terminate Process" onclick="terminateProcess(${s.Pid})">❌</button>
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
    
    return `
      <tr>
        <td>${l.Time}</td>
        <td>
          <div class="process-cell">
            <span style="font-size: 18px;">🛡️</span>
            <div class="process-info">
              <span class="process-name">${l.ProcessName}</span>
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
          <button class="action-btn" title="Quick Whitelist App" onclick="quickWhitelist('${l.Path}')">➕</button>
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

function initChart() {
  chartCanvas = document.getElementById('bandwidthChart');
  ctx = chartCanvas.getContext('2d');
  window.addEventListener('resize', resizeCanvas);
  resizeCanvas();
}

function resizeCanvas() {
  if (!chartCanvas) return;
  const rect = chartCanvas.parentElement.getBoundingClientRect();
  chartCanvas.width = rect.width;
  chartCanvas.height = 300;
  drawChart();
}

function drawChart() {
  if (!ctx) return;
  
  const w = chartCanvas.width;
  const h = chartCanvas.height;
  ctx.clearRect(0, 0, w, h);

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

  // Find max value in history to scale
  const maxVal = Math.max(...rxHistory, ...txHistory, 10); // Minimum scale of 10 KB/s

  // Helper to get coordinates
  function getX(index) {
    return (w / (maxHistory - 1)) * index;
  }

  function getY(value) {
    return h - (h - 40) * (value / maxVal) - 20;
  }

  // Draw Rx (Download) Area & Line
  drawPath(rxHistory, 'rgba(0, 255, 204, 0.15)', 'rgba(0, 255, 204, 1)', 2);

  // Draw Tx (Upload) Area & Line
  drawPath(txHistory, 'rgba(138, 43, 226, 0.15)', 'rgba(138, 43, 226, 1)', 2);

  function drawPath(history, fillGradient, strokeColor, lineWidth) {
    ctx.beginPath();
    ctx.moveTo(getX(0), getY(history[0]));

    for (let i = 1; i < maxHistory; i++) {
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
    ctx.lineTo(getX(maxHistory - 1), h);
    ctx.lineTo(getX(0), h);
    ctx.closePath();
    ctx.fillStyle = fillGradient;
    ctx.fill();
  }

  // Draw legend labels on chart
  ctx.font = '12px Outfit';
  ctx.fillStyle = '#00ffcc';
  ctx.fillText(`Down: ${formatSpeed(statusData.rxSpeed)}`, 20, 30);
  ctx.fillStyle = '#8a2be2';
  ctx.fillText(`Up: ${formatSpeed(statusData.txSpeed)}`, 20, 50);
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
    return `
      <div class="bandwidth-item">
        <div class="bandwidth-header">
          <span class="bandwidth-app">${a.name}</span>
          <span style="color: var(--text-secondary);">${a.count} active connections</span>
        </div>
        <div class="bandwidth-track">
          <div class="bandwidth-bar" style="width: ${widthPercentage}%;"></div>
        </div>
      </div>
    `;
  }).join('');
}
