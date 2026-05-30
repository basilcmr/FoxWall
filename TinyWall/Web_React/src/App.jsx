import React, { useState, useEffect } from 'react';
import { Shield, ShieldAlert, Activity, TrendingUp, LineChart, Zap, AlertTriangle, Power } from 'lucide-react';
import SocketsFeed from './components/SocketsFeed';
import BandwidthChart from './components/BandwidthChart';
import FirewallLogs from './components/FirewallLogs';
import ProcessAnalytics from './components/ProcessAnalytics';
import PowerScheduler from './components/PowerScheduler';
import { CopyOptionsModal, SearchOptionsModal, ChartClickDetailModal } from './components/Modals';

export default function App() {
  const [activeTab, setActiveTab] = useState('sockets');
  const [socketData, setSocketData] = useState([]);
  const [logData, setLogData] = useState([]);
  const [statusData, setStatusData] = useState({ mode: 'Normal', locked: false, rxSpeed: 0, txSpeed: 0, panicActive: false, version: '' });
  // [FoxWall Enhancement] - Network Adapter Filter View State (Physical vs. All)
  const [adapterView, setAdapterView] = useState('physical');
  // [FoxWall Enhancement] - End of Network Adapter Filter View State

  // Lookback filter states
  const [lookbackNum, setLookbackNum] = useState(5);
  const [lookbackUnit, setLookbackUnit] = useState('m');
  const [customRangeActive, setCustomRangeActive] = useState(false);
  const [detailedTooltipCheck, setDetailedTooltipCheck] = useState(true);

  // Custom date/time picker states
  const [customDateStart, setCustomDateStart] = useState('');
  const [customDateEnd, setCustomDateEnd] = useState('');
  const [customTimeStart, setCustomTimeStart] = useState('00:00');
  const [customTimeEnd, setCustomTimeEnd] = useState('23:59');

  // Analytics history data points
  const [analyticsPoints, setAnalyticsPoints] = useState([]);

  // Modal control states
  const [copyModalData, setCopyModalData] = useState({ isOpen: false, name: '', path: '' });
  const [searchModalData, setSearchModalData] = useState({ isOpen: false, name: '', path: '', appName: '' });
  const [clickModalData, setClickModalData] = useState({ isOpen: false, point: null });

  // Floating Toast Notifications state
  const [toastMessage, setToastMessage] = useState('');
  const [toastVisible, setToastVisible] = useState(false);

  // Initialize dates
  useEffect(() => {
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

    setCustomDateStart(toLocalDate(oneHourAgo));
    setCustomDateEnd(toLocalDate(now));
    setCustomTimeStart(toLocalTime(oneHourAgo));
    setCustomTimeEnd(toLocalTime(now));
  }, []);

  // Poll server data
  useEffect(() => {
    const pollData = async () => {
      try {
        const promises = [
          fetch('/api/status').then(r => r.json()),
          fetch('/api/connections').then(r => r.json()),
          fetch('/api/logs').then(r => r.json())
        ];

        // Fetch normal lookback timeline dynamically if custom is not active
        if (activeTab === 'analytics' && !customRangeActive) {
          const range = `${lookbackNum}${lookbackUnit}`;
          promises.push(fetch(`/api/analytics/history?range=${range}`).then(r => r.json()));
        }

        const results = await Promise.all(promises);
        setStatusData(results[0]);
        setSocketData(results[1]);
        setLogData(results[2]);

        if (results[3]) {
          setAnalyticsPoints(results[3]);
        }
      } catch (err) {
        console.error("Failed to connect to local C# API:", err);
      }
    };

    pollData();
    const interval = setInterval(pollData, 1500);
    return () => clearInterval(interval);
  }, [activeTab, lookbackNum, lookbackUnit, customRangeActive]);

  // Fetch custom range history when requested
  const fetchCustomHistory = async () => {
    try {
      const start = `${customDateStart}T${customTimeStart}:00`;
      const end = `${customDateEnd}T${customTimeEnd}:59`;
      const url = `/api/analytics/history?range=custom&start=${encodeURIComponent(start)}&end=${encodeURIComponent(end)}`;
      const res = await fetch(url);
      const points = await res.json();
      setAnalyticsPoints(points);
    } catch (err) {
      console.error("Failed to fetch custom analytics history:", err);
    }
  };

  useEffect(() => {
    if (activeTab === 'analytics' && customRangeActive) {
      fetchCustomHistory();
    }
  }, [activeTab, customRangeActive]);

  const showToast = (msg) => {
    setToastMessage(msg);
    setToastVisible(true);
    setTimeout(() => {
      setToastVisible(false);
    }, 2500);
  };

  // Terminate Process
  const terminateProcess = async (pid) => {
    if (window.confirm(`Are you absolutely sure you want to terminate process ID ${pid}?`)) {
      try {
        const res = await fetch(`/api/action/terminate?pid=${pid}`, { method: 'POST' });
        if (res.ok) {
          showToast("Process terminated successfully!");
          // Refresh socket list immediately
          const refresh = await fetch('/api/connections').then(r => r.json());
          setSocketData(refresh);
        } else {
          alert("Could not terminate process.");
        }
      } catch (err) {
        alert("Error terminating process.");
      }
    }
  };

  // Quick Whitelist
  const quickWhitelist = async (path) => {
    if (window.confirm(`Do you want to whitelist and allow network access for ${path}?`)) {
      try {
        const res = await fetch(`/api/action/whitelist?path=${encodeURIComponent(path)}`, { method: 'POST' });
        if (res.ok) {
          showToast("Whitelisted successfully!");
          // Refresh log lists immediately
          const refresh = await fetch('/api/logs').then(r => r.json());
          setLogData(refresh);
        } else {
          alert("Whitelisting failed.");
        }
      } catch (err) {
        alert("Error whitelisting.");
      }
    }
  };

  // Global Panic Switch
  const togglePanicSwitch = async () => {
    try {
      const res = await fetch('/api/action/panic', { method: 'POST' });
      const result = await res.json();
      setStatusData(prev => ({ ...prev, panicActive: result.active }));
      showToast(result.active ? "Global Panic block active!" : "Global Guard restored.");
    } catch (err) {
      alert("Could not trigger panic switch.");
    }
  };

  // VirusTotal Lookup
  const searchVirusTotal = (path, fileHash) => {
    if (fileHash && fileHash.length > 0 && fileHash !== "null" && fileHash !== "undefined") {
      window.open(`https://www.virustotal.com/gui/search/${fileHash}`, '_blank');
      showToast("Searching VirusTotal with C# Computed SHA1 Hash!");
    } else {
      const filename = path ? path.split('\\').pop() : path;
      window.open(`https://www.virustotal.com/gui/search/${encodeURIComponent(filename)}`, '_blank');
      showToast("Searching VirusTotal with process filename...");
    }
  };

  // Formatting rates
  const formatSpeed = (bytesPerSec) => {
    const kb = bytesPerSec / 1024;
    if (kb > 1024) {
      return `${(kb / 1024).toFixed(1)} MiB/s`;
    }
    return `${kb.toFixed(1)} KiB/s`;
  };

  // Stats Card labels durations
  const getBlockedCardDurationLabel = () => {
    if (customRangeActive) return 'Custom Range';
    const label = lookbackUnit === 'm' ? 'Min' : (lookbackUnit === 'h' ? 'Hours' : 'Days');
    return `Last ${lookbackNum} ${label}`;
  };

  // [FoxWall Enhancement] - Is Firewall Guard Active
  const isGuardActive = statusData.mode !== 'Disabled';
  // [FoxWall Enhancement] - End of Is Firewall Guard Active

  return (
    <>
      {/* Premium Header */}
      <header>
        <div className="logo-container">
          <div className="logo-icon">
            <ShieldAlert style={{ width: '18px', height: '18px', color: 'white' }} />
          </div>
          <div>
            <h1 style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              FoxWall 
              <span id="appVersion" style={{ fontSize: '12px', opacity: 0.6, fontWeight: 400, marginTop: '4px' }}>
                {statusData.version ? `v${statusData.version}` : ''}
              </span>
            </h1>
            <div className="logo-sub">Security Monitor</div>
          </div>
        </div>
        <div className="header-actions">
          {/* [FoxWall Enhancement] - Network Adapter Filter Toggle Switch */}
          <div className="filter-btn-group" style={{ marginRight: '12px', border: '1px solid var(--border-color)', padding: '2px', borderRadius: '8px' }}>
            <button
              className={`filter-btn ${adapterView === 'physical' ? 'active' : ''}`}
              style={{ fontSize: '11px', padding: '6px 12px', height: 'auto', borderRadius: '6px' }}
              onClick={() => setAdapterView('physical')}
              title="Filter traffic to physical network adapters only (prevents virtual bridge / VPN duplication)"
            >
              Physical Only
            </button>
            <button
              className={`filter-btn ${adapterView === 'all' ? 'active' : ''}`}
              style={{ fontSize: '11px', padding: '6px 12px', height: 'auto', borderRadius: '6px' }}
              onClick={() => setAdapterView('all')}
              title="Show total combined bandwidth across all adapters (including virtual adapters and loopbacks)"
            >
              All Adapters
            </button>
          </div>
          {/* [FoxWall Enhancement] - End of Network Adapter Filter Toggle Switch */}

          {/* [FoxWall Enhancement] - Guard Status Badge (Active vs. Deactivated) */}
          <div className="status-badge" style={{
            borderColor: isGuardActive ? 'var(--success-color)' : 'var(--danger-color)',
            color: isGuardActive ? 'var(--success-color)' : 'var(--danger-color)',
            boxShadow: isGuardActive ? '0 0 10px var(--success-glow)' : '0 0 10px var(--danger-glow)',
            background: isGuardActive ? '#00ffcc1a' : 'rgba(255, 51, 102, 0.1)'
          }}>
            <div className="status-dot" style={{
              backgroundColor: isGuardActive ? 'var(--success-color)' : 'var(--danger-color)',
              animation: isGuardActive ? '1.5s infinite pulse' : 'none'
            }}></div>
            <span id="statusText">{isGuardActive ? 'Active Guard' : 'Guard Deactivated'}</span>
          </div>
          {/* [FoxWall Enhancement] - End of Guard Status Badge */}
        </div>
      </header>

      {/* Dashboard Container */}
      <div className="dashboard-container">
        
        {/* Sidebar Navigation */}
        <div className="sidebar">
          <div className={`nav-item ${activeTab === 'sockets' ? 'active' : ''}`} onClick={() => setActiveTab('sockets')}>
            <Activity className="nav-icon" />
            <span>Live Sockets Feed</span>
          </div>
          <div className={`nav-item ${activeTab === 'analytics' ? 'active' : ''}`} onClick={() => setActiveTab('analytics')}>
            <TrendingUp className="nav-icon" />
            <span>Bandwidth Analytics</span>
          </div>
          <div className={`nav-item ${activeTab === 'logs' ? 'active' : ''}`} onClick={() => setActiveTab('logs')}>
            <Shield className="nav-icon" />
            <span>Firewall Events</span>
          </div>
          <div className={`nav-item ${activeTab === 'process-analytics' ? 'active' : ''}`} onClick={() => setActiveTab('process-analytics')}>
            <LineChart className="nav-icon" />
            <span>Process Analytics</span>
          </div>
          {/* [FoxWall Enhancement] - Start of Power Scheduler Tab */}
          <div className={`nav-item ${activeTab === 'power-scheduler' ? 'active' : ''}`} onClick={() => setActiveTab('power-scheduler')}>
            <Power className="nav-icon" />
            <span>Power Scheduler</span>
          </div>
          {/* [FoxWall Enhancement] - End of Power Scheduler Tab */}
        </div>

        {/* Workspace */}
        <div className="workspace">
          
          {/* Stats Summary Row */}
          <div className="stats-row">
            <div className="stat-card">
              <div className="stat-title">Total Active Sockets</div>
              <div className="stat-value">{socketData.length}</div>
              <div className="stat-rate in">{formatSpeed(adapterView === 'physical' ? (statusData.rxSpeedPhysical ?? statusData.rxSpeed) : statusData.rxSpeed)} Down</div>
            </div>
            <div className="stat-card">
              <div className="stat-title">Blocked ({getBlockedCardDurationLabel()})</div>
              <div className="stat-value" style={{ color: 'var(--danger-color)' }}>
                {logData.filter(e => e.Action === 'Blocked' || e.State === 'Blocked').length}
              </div>
              <div className="stat-rate out">{formatSpeed(adapterView === 'physical' ? (statusData.txSpeedPhysical ?? statusData.txSpeed) : statusData.txSpeed)} Up</div>
            </div>
            <div className="stat-card">
              <div className="stat-title">Security State</div>
              <div className="stat-value" style={{ fontSize: '22px', paddingTop: '5px' }}>{statusData.mode} Mode</div>
              <div className="stat-rate" style={{ color: 'var(--text-secondary)' }}>{statusData.locked ? 'Locked' : 'Unlocked'}</div>
            </div>
          </div>

          {/* TAB 1: Live Sockets Feed */}
          {activeTab === 'sockets' && (
            <SocketsFeed
              socketData={socketData}
              logData={logData}
              onOpenCopy={(name, path) => setCopyModalData({ isOpen: true, name, path })}
              onOpenSearch={(name, path, appName) => setSearchModalData({ isOpen: true, name, path, appName })}
              searchVirusTotal={searchVirusTotal}
              terminateProcess={terminateProcess}
            />
          )}

          {/* TAB 2: Bandwidth Analytics */}
          {activeTab === 'analytics' && (
            <BandwidthChart
              analyticsPoints={analyticsPoints}
              socketData={socketData}
              logData={logData}
              onOpenCopy={(name, path) => setCopyModalData({ isOpen: true, name, path })}
              onOpenSearch={(name, path, appName) => setSearchModalData({ isOpen: true, name, path, appName })}
              searchVirusTotal={searchVirusTotal}
              formatSpeed={formatSpeed}
              detailedTooltipCheck={detailedTooltipCheck}
              setDetailedTooltipCheck={setDetailedTooltipCheck}
              lookbackNum={lookbackNum}
              setLookbackNum={setLookbackNum}
              lookbackUnit={lookbackUnit}
              setLookbackUnit={setLookbackUnit}
              customRangeActive={customRangeActive}
              setCustomRangeActive={setCustomRangeActive}
              customDateStart={customDateStart}
              setCustomDateStart={setCustomDateStart}
              customDateEnd={customDateEnd}
              setCustomDateEnd={setCustomDateEnd}
              customTimeStart={customTimeStart}
              setCustomTimeStart={setCustomTimeStart}
              customTimeEnd={customTimeEnd}
              setCustomTimeEnd={setCustomTimeEnd}
              onApplyCustomRange={fetchCustomHistory}
              onOpenClickModal={(point) => setClickModalData({ isOpen: true, point })}
              rxSpeed={adapterView === 'physical' ? (statusData.rxSpeedPhysical ?? statusData.rxSpeed) : statusData.rxSpeed}
              txSpeed={adapterView === 'physical' ? (statusData.txSpeedPhysical ?? statusData.txSpeed) : statusData.txSpeed}
              adapterView={adapterView}
            />
          )}

          {/* TAB 3: Firewall Events (Logs) */}
          {activeTab === 'logs' && (
            <FirewallLogs
              logData={logData}
              socketData={socketData}
              onOpenCopy={(name, path) => setCopyModalData({ isOpen: true, name, path })}
              onOpenSearch={(name, path, appName) => setSearchModalData({ isOpen: true, name, path, appName })}
              searchVirusTotal={searchVirusTotal}
              quickWhitelist={quickWhitelist}
            />
          )}

          {/* TAB 4: Process Analytics */}
          {activeTab === 'process-analytics' && (
            <ProcessAnalytics
              analyticsPoints={analyticsPoints}
              socketData={socketData}
              logData={logData}
              formatSpeed={formatSpeed}
            />
          )}

          {/* [FoxWall Enhancement] - Start of Power Scheduler Component */}
          {activeTab === 'power-scheduler' && (
            <PowerScheduler showToast={showToast} />
          )}
          {/* [FoxWall Enhancement] - End of Power Scheduler Component */}

        </div>
      </div>

      {/* Floating Toast Notification */}
      <div id="appToast" className={toastVisible ? 'show' : ''}>
        {toastMessage}
      </div>

      {/* Overlapping Modals */}
      <CopyOptionsModal
        isOpen={copyModalData.isOpen}
        onClose={() => setCopyModalData({ isOpen: false, name: '', path: '' })}
        name={copyModalData.name}
        path={copyModalData.path}
        showToast={showToast}
      />

      <SearchOptionsModal
        isOpen={searchModalData.isOpen}
        onClose={() => setSearchModalData({ isOpen: false, name: '', path: '', appName: '' })}
        name={searchModalData.name}
        path={searchModalData.path}
        appName={searchModalData.appName}
      />

      <ChartClickDetailModal
        isOpen={clickModalData.isOpen}
        onClose={() => setClickModalData({ isOpen: false, point: null })}
        point={clickModalData.point}
        socketData={socketData}
        logData={logData}
        onOpenCopy={(name, path) => setCopyModalData({ isOpen: true, name, path })}
        onOpenSearch={(name, path, appName) => setSearchModalData({ isOpen: true, name, path, appName })}
        searchVirusTotal={searchVirusTotal}
        formatSpeed={formatSpeed}
      />
    </>
  );
}
