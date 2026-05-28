import React, { useState } from 'react';
import { Search, Shield, PlusCircle } from 'lucide-react';
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

  // Filter
  const filtered = logData.filter((l) => {
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
    <div className="panel">
      <div className="panel-header">
        <div className="panel-title">Recent Firewall Log Actions</div>
        <div className="controls-box">
          <div className="search-input-wrapper">
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
                  No recent events found.
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
