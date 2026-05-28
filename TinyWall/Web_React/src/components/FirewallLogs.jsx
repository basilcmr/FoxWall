import React, { useState, useEffect } from 'react';
import { Search, Shield, PlusCircle, Calendar } from 'lucide-react';
import ProcessActions from './ProcessActions';

export default function FirewallLogs({
  logData,
  socketData,
  onOpenCopy,
  onOpenSearch,
  searchVirusTotal,
  quickWhitelist
}) {
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState('all');
  const [sortCol, setSortCol] = useState('Time');
  const [sortAsc, setSortAsc] = useState(false);

  // Lookback states for firewall logs
  const [lookbackNum, setLookbackNum] = useState(30); // Default to 30 min lookup window for logs
  const [lookbackUnit, setLookbackUnit] = useState('m');
  const [customRangeActive, setCustomRangeActive] = useState(false);

  // Calendar dates
  const [customDateStart, setCustomDateStart] = useState('');
  const [customDateEnd, setCustomDateEnd] = useState('');
  const [customTimeStart, setCustomTimeStart] = useState('00:00');
  const [customTimeEnd, setCustomTimeEnd] = useState('23:59');

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

  // Filter logs by date range window
  const getRangeLimit = () => {
    if (customRangeActive) {
      const start = new Date(`${customDateStart}T${customTimeStart}:00`);
      const end = new Date(`${customDateEnd}T${customTimeEnd}:59`);
      return { start, end };
    } else {
      const end = new Date();
      let start = new Date(end.getTime());
      if (lookbackUnit === 'm') start.setMinutes(start.getMinutes() - lookbackNum);
      else if (lookbackUnit === 'h') start.setHours(start.getHours() - lookbackNum);
      else if (lookbackUnit === 'd') start.setDate(start.getDate() - lookbackNum);
      return { start, end };
    }
  };

  const { start, end } = getRangeLimit();
  const timeFiltered = logData.filter((l) => {
    const logTime = new Date(l.Time.replace(' ', 'T'));
    return logTime >= start && logTime <= end;
  });

  // Search & Action filter
  const filtered = timeFiltered.filter((l) => {
    const q = searchQuery.toLowerCase();
    const matchesSearch =
      (l.ProcessName && l.ProcessName.toLowerCase().includes(q)) ||
      (l.Path && l.Path.toLowerCase().includes(q)) ||
      (l.RemoteAddress && l.RemoteAddress.toLowerCase().includes(q));

    if (filterType === 'blocked') return matchesSearch && l.Action === 'Blocked';
    if (filterType === 'allowed') return matchesSearch && l.Action === 'Allowed';
    return matchesSearch;
  });

  // Sort
  const sorted = [...filtered].sort((a, b) => {
    const valA = a[sortCol];
    const valB = b[sortCol];
    if (typeof valA === 'string') {
      return sortAsc ? valA.localeCompare(valB) : valB.localeCompare(valA);
    }
    return sortAsc ? valA - valB : valB - valA;
  });

  const handleSort = (col) => {
    if (sortCol === col) {
      setSortAsc(!sortAsc);
    } else {
      setSortCol(col);
      setSortAsc(true);
    }
  };

  return (
    <div className="panel" style={{ gap: '15px' }}>
      
      {/* Search and Category Filter Toolbar */}
      <div className="panel-header" style={{ flexWrap: 'wrap', gap: '15px' }}>
        <div className="panel-title">Recent Firewall Log Actions</div>
        <div className="controls-box" style={{ gap: '15px' }}>
          <div className="search-input-wrapper" style={{ minWidth: '220px' }}>
            <Search className="search-icon" size={16} />
            <input
              type="text"
              className="search-input"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Filter logs..."
            />
          </div>
          <div className="filter-btn-group">
            <button
              className={`filter-btn ${filterType === 'all' ? 'active' : ''}`}
              onClick={() => setFilterType('all')}
            >
              All Events
            </button>
            <button
              className={`filter-btn ${filterType === 'blocked' ? 'active' : ''}`}
              onClick={() => setFilterType('blocked')}
            >
              Blocked
            </button>
            <button
              className={`filter-btn ${filterType === 'allowed' ? 'active' : ''}`}
              onClick={() => setFilterType('allowed')}
            >
              Allowed
            </button>
          </div>
        </div>
      </div>

      {/* Date Range History Toolbar */}
      <div className="controls-box" style={{ padding: '6px 0', borderTop: '1px solid var(--border-color)', borderBottom: '1px solid var(--border-color)', gap: '15px', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', background: 'rgba(255,255,255,0.02)', padding: '6px 12px', borderRadius: 'var(--border-radius)', border: '1px solid var(--border-color)', flexWrap: 'wrap' }}>
          <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)' }}>Log Lookback:</span>
          
          <div className="filter-btn-group" style={{ border: 'none', padding: 0, background: 'transparent', gap: '4px' }}>
            {[5, 15, 30, 60].map(n => (
              <button
                key={n}
                className={`filter-btn ${lookbackNum === n && !customRangeActive ? 'active' : ''}`}
                style={{ padding: '4px 10px' }}
                onClick={() => {
                  setLookbackNum(n);
                  setCustomRangeActive(false);
                }}
              >
                {n}
              </button>
            ))}
          </div>

          <input
            type="number"
            className="search-input"
            value={lookbackNum}
            onChange={(e) => {
              const val = parseInt(e.target.value);
              if (val > 0) {
                setLookbackNum(val);
                setCustomRangeActive(false);
              }
            }}
            min="1"
            style={{ width: '60px', minWidth: '60px', padding: '4px 8px', textAlign: 'center' }}
          />

          <div className="filter-btn-group" style={{ border: 'none', padding: 0, background: 'transparent', gap: '4px' }}>
            {[
              { k: 'm', label: 'Min' },
              { k: 'h', label: 'Hour' },
              { k: 'd', label: 'Days' }
            ].map(u => (
              <button
                key={u.k}
                className={`filter-btn ${lookbackUnit === u.k && !customRangeActive ? 'active' : ''}`}
                style={{ padding: '4px 10px' }}
                onClick={() => {
                  setLookbackUnit(u.k);
                  setCustomRangeActive(false);
                }}
              >
                {u.label}
              </button>
            ))}
          </div>
        </div>

        <button
          className={`filter-btn ${customRangeActive ? 'active' : ''}`}
          style={{ padding: '8px 16px', border: '1px solid var(--border-color)', display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          onClick={() => setCustomRangeActive(!customRangeActive)}
        >
          <Calendar size={14} /> Custom Date Range
        </button>
      </div>

      {/* Mini Calendar Pickers */}
      {customRangeActive && (
        <div style={{ display: 'flex', alignItems: 'center', gap: '15px', background: 'rgba(22, 22, 34, 0.4)', padding: '12px 20px', borderRadius: 'var(--border-radius)', border: '1px solid var(--border-color)', flexWrap: 'wrap', marginTop: '-5px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ fontSize: '13px', color: 'var(--text-secondary)', fontWeight: 500 }}>Dates:</span>
            <input type="date" className="search-input" value={customDateStart} onChange={e => setCustomDateStart(e.target.value)} style={{ padding: '6px 10px', fontSize: '13px', width: '140px', minWidth: '140px', textAlign: 'center' }} />
            <span style={{ color: 'var(--text-secondary)' }}>to</span>
            <input type="date" className="search-input" value={customDateEnd} onChange={e => setCustomDateEnd(e.target.value)} style={{ padding: '6px 10px', fontSize: '13px', width: '140px', minWidth: '140px', textAlign: 'center' }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ fontSize: '13px', color: 'var(--text-secondary)', fontWeight: 500 }}>Times:</span>
            <input type="time" className="search-input" value={customTimeStart} onChange={e => setCustomTimeStart(e.target.value)} style={{ padding: '6px 10px', fontSize: '13px', width: '110px', minWidth: '110px', textAlign: 'center' }} />
            <span style={{ color: 'var(--text-secondary)' }}>to</span>
            <input type="time" className="search-input" value={customTimeEnd} onChange={e => setCustomTimeEnd(e.target.value)} style={{ padding: '6px 10px', fontSize: '13px', width: '110px', minWidth: '110px', textAlign: 'center' }} />
          </div>
        </div>
      )}

      {/* Sockets Table */}
      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th onClick={() => handleSort('Time')}>Timestamp</th>
              <th onClick={() => handleSort('ProcessName')}>Application</th>
              <th onClick={() => handleSort('Protocol')}>Protocol</th>
              <th onClick={() => handleSort('Direction')}>Direction</th>
              <th onClick={() => handleSort('LocalAddress')}>Local Address</th>
              <th onClick={() => handleSort('RemoteAddress')}>Remote Address</th>
              <th onClick={() => handleSort('Action')}>Action</th>
              <th>Whitelist</th>
            </tr>
          </thead>
          <tbody>
            {sorted.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
                  No firewall events found in this range.
                </td>
              </tr>
            ) : (
              sorted.map((l, idx) => {
                const isBlocked = l.Action === 'Blocked';
                const badgeClass = isBlocked ? 'blocked' : 'active';
                return (
                  <tr key={idx}>
                    <td>{l.Time}</td>
                    <td>
                      <div className="process-cell" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                        <Shield size={16} style={{ color: 'var(--text-secondary)' }} />
                        <div className="process-info">
                          <span className="process-name" style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap' }}>
                            {l.ProcessName}
                            <ProcessActions
                              processName={l.ProcessName}
                              path={l.Path}
                              fileHash={l.FileHash}
                              socketData={socketData}
                              logData={logData}
                              onOpenCopy={onOpenCopy}
                              onOpenSearch={onOpenSearch}
                              searchVirusTotal={searchVirusTotal}
                            />
                          </span>
                          <span className="process-pid">PID: {l.Pid}</span>
                        </div>
                      </div>
                    </td>
                    <td><span className="protocol-badge">{l.Protocol}</span></td>
                    <td>{l.Direction}</td>
                    <td>{l.LocalAddress}:{l.LocalPort}</td>
                    <td>{l.RemoteAddress}:{l.RemotePort}</td>
                    <td><span className={`state-badge ${badgeClass}`}>{l.Action}</span></td>
                    <td>
                      <button className="action-btn" title="Quick Whitelist App" onClick={() => quickWhitelist(l.Path)}>
                        <PlusCircle size={14} style={{ color: 'white' }} />
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
