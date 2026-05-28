import React, { useState } from 'react';
import { Search, Cpu, ShieldAlert, Trash2 } from 'lucide-react';
import ProcessActions from './ProcessActions';

const renderRemoteAddress = (ip, port) => {
  if (!ip || ip === '*' || ip === '0.0.0.0') return '*';
  
  const isPrivate = ip.startsWith('127.') || 
                    ip.startsWith('192.168.') || 
                    ip.startsWith('10.') || 
                    ip.startsWith('169.254.') || 
                    ip.startsWith('255.255.255.255') || 
                    (ip.startsWith('172.') && parseInt(ip.split('.')[1]) >= 16 && parseInt(ip.split('.')[1]) <= 31);

  if (isPrivate) {
    return (
      <span style={{ color: 'var(--text-secondary)' }} title="Private/Local Network IP">
        {ip}:{port}
      </span>
    );
  }

  return (
    <a
      href={`https://ipinfo.io/${ip}`}
      target="_blank"
      rel="noopener noreferrer"
      style={{
        color: 'var(--accent-color)',
        textDecoration: 'none',
        borderBottom: '1px dashed rgba(224, 64, 251, 0.4)',
        cursor: 'pointer'
      }}
      title="Lookup IP Info (External Service)"
      onMouseOver={(e) => { e.currentTarget.style.borderBottom = '1px solid var(--accent-color)'; }}
      onMouseOut={(e) => { e.currentTarget.style.borderBottom = '1px dashed rgba(224, 64, 251, 0.4)'; }}
    >
      {ip}:{port}
    </a>
  );
};

export default function SocketsFeed({
  socketData,
  logData,
  onOpenCopy,
  onOpenSearch,
  searchVirusTotal,
  terminateProcess
}) {
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState('all');
  const [sortCol, setSortCol] = useState('time');
  const [sortAsc, setSortAsc] = useState(false);

  // Filter
  const filtered = socketData.filter((s) => {
    const q = searchQuery.toLowerCase();
    const matchesSearch =
      (s.ProcessName && s.ProcessName.toLowerCase().includes(q)) ||
      (s.Path && s.Path.toLowerCase().includes(q)) ||
      (s.RemoteAddress && s.RemoteAddress.toLowerCase().includes(q)) ||
      (s.LocalAddress && s.LocalAddress.toLowerCase().includes(q));

    if (filterType === 'listening') return matchesSearch && s.State === 'Listening';
    if (filterType === 'active') return matchesSearch && s.State !== 'Listening';
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
        <div className="panel-title">Active Connections & Sockets</div>
        <div className="controls-box">
          <div className="search-input-wrapper">
            <Search className="search-icon" size={16} />
            <input
              type="text"
              className="search-input"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by name, path, or IP..."
            />
          </div>
          <div className="filter-btn-group">
            <button
              className={`filter-btn ${filterType === 'all' ? 'active' : ''}`}
              onClick={() => setFilterType('all')}
            >
              All
            </button>
            <button
              className={`filter-btn ${filterType === 'active' ? 'active' : ''}`}
              onClick={() => setFilterType('active')}
            >
              Active
            </button>
            <button
              className={`filter-btn ${filterType === 'listening' ? 'active' : ''}`}
              onClick={() => setFilterType('listening')}
            >
              Listening
            </button>
          </div>
        </div>
      </div>

      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th onClick={() => handleSort('ProcessName')}>Application</th>
              <th onClick={() => handleSort('Protocol')}>Protocol</th>
              <th onClick={() => handleSort('LocalAddress')}>Local Address</th>
              <th onClick={() => handleSort('RemoteAddress')}>Remote Address</th>
              <th onClick={() => handleSort('State')}>State</th>
              <th onClick={() => handleSort('Time')}>Modified Time</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {sorted.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
                  No sockets matching search or filter query.
                </td>
              </tr>
            ) : (
              sorted.map((s, idx) => {
                const isListen = s.State === 'Listening';
                const badgeClass = isListen ? 'listening' : 'active';
                return (
                  <tr key={idx}>
                    <td>
                      <div className="process-cell" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                        <Cpu size={16} style={{ color: 'var(--text-secondary)' }} />
                        <div className="process-info">
                          <span className="process-name" style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap' }}>
                            {s.ProcessName}
                            <ProcessActions
                              processName={s.ProcessName}
                              path={s.Path}
                              fileHash={s.FileHash}
                              socketData={socketData}
                              logData={logData}
                              onOpenCopy={onOpenCopy}
                              onOpenSearch={onOpenSearch}
                              searchVirusTotal={searchVirusTotal}
                            />
                          </span>
                          <span className="process-pid">PID: {s.Pid}</span>
                        </div>
                      </div>
                    </td>
                    <td><span className="protocol-badge">{s.Protocol}</span></td>
                    <td>{s.LocalAddress}:{s.LocalPort}</td>
                    <td>{isListen ? '*' : renderRemoteAddress(s.RemoteAddress, s.RemotePort)}</td>
                    <td><span className={`state-badge ${badgeClass}`}>{s.State}</span></td>
                    <td>{s.Time}</td>
                    <td>
                      <button className="action-btn" title="VirusTotal Lookup" onClick={() => searchVirusTotal(s.Path, s.FileHash)} style={{ marginRight: '4px' }}>
                        <ShieldAlert size={14} style={{ color: 'white' }} />
                      </button>
                      <button className="action-btn terminate" title="Terminate Process" onClick={() => terminateProcess(s.Pid)}>
                        <Trash2 size={14} style={{ color: 'white' }} />
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
