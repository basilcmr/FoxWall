import React, { useState, useEffect } from 'react';
import { Copy, Search, BarChart2, ShieldAlert, X } from 'lucide-react';
import ProcessActions from './ProcessActions';

// 1. Clipboard Copy Options Modal
export function CopyOptionsModal({ isOpen, onClose, name, path, showToast }) {
  const [copyField, setCopyField] = useState('name');

  if (!isOpen) return null;

  const pathPreview = path || name;
  const combinedPreview = path ? `${name} - ${path}` : name;

  const handleCopy = () => {
    let copiedText = '';
    if (copyField === 'name') copiedText = name;
    else if (copyField === 'path') copiedText = pathPreview;
    else copiedText = combinedPreview;

    navigator.clipboard.writeText(copiedText).then(() => {
      showToast("Copied selection to clipboard!");
      onClose();
    }).catch(() => {
      alert("Failed to copy data.");
    });
  };

  return (
    <div id="copyOptionsModal" className="modal-overlay" style={{ display: 'flex' }}>
      <div className="modal-card">
        <div className="modal-header">
          <h3 className="modal-title" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Copy size={16} className="nav-icon" style={{ margin: 0 }} />
            Copy to Clipboard Options
          </h3>
          <span className="modal-close" onClick={onClose}><X size={20} /></span>
        </div>
        <div className="modal-body" style={{ display: 'flex', flexDirection: 'column', gap: '12px', padding: '20px 0' }}>
          <label className="modal-option-row">
            <input type="radio" name="copyField" value="name" checked={copyField === 'name'} onChange={() => setCopyField('name')} />
            <div className="option-info">
              <span className="option-title">Process Name</span>
              <span className="option-desc">{name}</span>
            </div>
          </label>
          <label className="modal-option-row">
            <input type="radio" name="copyField" value="path" checked={copyField === 'path'} onChange={() => setCopyField('path')} />
            <div className="option-info">
              <span className="option-title">Executable Full Path</span>
              <span className="option-desc">{pathPreview}</span>
            </div>
          </label>
          <label className="modal-option-row">
            <input type="radio" name="copyField" value="combined" checked={copyField === 'combined'} onChange={() => setCopyField('combined')} />
            <div className="option-info">
              <span className="option-title">Combined Details</span>
              <span className="option-desc">{combinedPreview}</span>
            </div>
          </label>
        </div>
        <div className="modal-footer" style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
          <button className="filter-btn" onClick={onClose}>Cancel</button>
          <button className="filter-btn" style={{ background: 'var(--accent-color)', color: 'white' }} onClick={handleCopy}>Copy to Clipboard</button>
        </div>
      </div>
    </div>
  );
}

// 2. Google Search Options Modal
export function SearchOptionsModal({ isOpen, onClose, name, path }) {
  const [searchField, setSearchField] = useState('name');
  const [usePrompt, setUsePrompt] = useState(true);
  const [promptQuery, setPromptQuery] = useState('');

  const fileName = path ? path.split('\\').pop().split('.').shift() : name;

  useEffect(() => {
    if (isOpen) {
      const term = searchField === 'name' ? name : fileName;
      setPromptQuery(`is ${term} safe legitimate or malware virus`);
    }
  }, [isOpen, searchField, name, fileName]);

  if (!isOpen) return null;

  const handleSearch = () => {
    let finalQuery = '';
    if (usePrompt && promptQuery.trim().length > 0) {
      finalQuery = promptQuery;
    } else {
      const term = searchField === 'name' ? name : fileName;
      finalQuery = `is ${term} safe legitimate or malware virus`;
    }
    window.open(`https://www.google.com/search?q=${encodeURIComponent(finalQuery)}`, '_blank');
    onClose();
  };

  return (
    <div id="searchOptionsModal" className="modal-overlay" style={{ display: 'flex' }}>
      <div className="modal-card">
        <div className="modal-header">
          <h3 className="modal-title" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Search size={16} className="nav-icon" style={{ margin: 0 }} />
            Search Google Options
          </h3>
          <span className="modal-close" onClick={onClose}><X size={20} /></span>
        </div>
        <div className="modal-body" style={{ display: 'flex', flexDirection: 'column', gap: '12px', padding: '20px 0' }}>
          <label className="modal-option-row">
            <input type="radio" name="searchField" value="name" checked={searchField === 'name'} onChange={() => setSearchField('name')} />
            <div className="option-info">
              <span className="option-title">Search Process Name</span>
              <span className="option-desc">{name}</span>
            </div>
          </label>
          <label className="modal-option-row">
            <input type="radio" name="searchField" value="path" checked={searchField === 'path'} onChange={() => setSearchField('path')} />
            <div className="option-info">
              <span className="option-title">Search File Name</span>
              <span className="option-desc">{fileName}</span>
            </div>
          </label>
          
          <div id="searchPromptContainer" style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '10px', paddingTop: '12px', borderTop: '1px solid var(--border-color)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: '12px', fontWeight: '600', color: 'white' }}>Search Query:</span>
              <label style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '11px', color: 'var(--text-secondary)', cursor: 'pointer', userSelect: 'none' }}>
                <input type="checkbox" checked={usePrompt} onChange={(e) => setUsePrompt(e.target.checked)} style={{ accentColor: 'var(--accent-color)', cursor: 'pointer' }} />
                Prompt Checkbox (Edit query)
              </label>
            </div>
            <input
              type="text"
              className="search-input"
              value={promptQuery}
              onChange={(e) => setPromptQuery(e.target.value)}
              disabled={!usePrompt}
              style={{ padding: '10px 14px', fontSize: '13px', color: 'white', opacity: usePrompt ? 1 : 0.5 }}
            />
          </div>
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }} className="modal-footer">
          <button className="filter-btn" onClick={onClose}>Cancel</button>
          <button className="filter-btn" style={{ background: 'var(--accent-color)', color: 'white' }} onClick={handleSearch}>Search Google</button>
        </div>
      </div>
    </div>
  );
}

// 3. Click Detail Breakdown Modal
export function ChartClickDetailModal({ isOpen, onClose, point, socketData, logData, onOpenCopy, onOpenSearch, searchVirusTotal, formatSpeed }) {
  if (!isOpen || !point) return null;

  const d = new Date(point.Time);
  const timeStr = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  const dateStr = d.toLocaleDateString();

  const rawTasks = point.PeakTask || 'System Service (Idle)';
  const tasks = rawTasks === 'Idle' || !rawTasks ? [] : rawTasks.split(';');

  return (
    <div id="chartClickDetailModal" className="modal-overlay" style={{ display: 'flex' }}>
      <div className="modal-card" style={{ maxWidth: '550px' }}>
        <div className="modal-header">
          <div>
            <h3 className="modal-title" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <BarChart2 size={16} className="nav-icon" style={{ margin: 0 }} />
              Historical Throughput Breakdown
            </h3>
            <span id="clickModalTimestamp" style={{ fontSize: '11px', color: 'var(--text-secondary)' }}>{dateStr} {timeStr}</span>
          </div>
          <span className="modal-close" onClick={onClose}><X size={20} /></span>
        </div>
        <div className="modal-body" style={{ padding: '20px 0', display: 'flex', flexDirection: 'column', gap: '15px' }}>
          <div style={{ display: 'flex', gap: '15px' }}>
            <div style={{ flex: 1, background: 'rgba(0,255,204,0.04)', border: '1px solid rgba(0,255,204,0.15)', padding: '12px', borderRadius: '8px', textAlign: 'center' }}>
              <span style={{ fontSize: '11px', color: 'var(--text-secondary)', textTransform: 'uppercase', fontWeight: 600, letterSpacing: '0.5px' }}>Total Download</span>
              <div id="clickModalRx" style={{ fontSize: '20px', fontWeight: 700, color: 'var(--success-color)', marginTop: '4px' }}>{formatSpeed(point.Rx)}</div>
            </div>
            <div style={{ flex: 1, background: 'rgba(138,43,226,0.04)', border: '1px solid rgba(138,43,226,0.15)', padding: '12px', borderRadius: '8px', textAlign: 'center' }}>
              <span style={{ fontSize: '11px', color: 'var(--text-secondary)', textTransform: 'uppercase', fontWeight: 600, letterSpacing: '0.5px' }}>Total Upload</span>
              <div id="clickModalTx" style={{ fontSize: '20px', fontWeight: 700, color: 'var(--accent-color)', marginTop: '4px' }}>{formatSpeed(point.Tx)}</div>
            </div>
          </div>
          
          <div className="table-wrapper">
            <table style={{ width: '100%' }}>
              <thead>
                <tr>
                  <th style={{ padding: '12px 16px', fontSize: '12px' }}>Active Task</th>
                  <th style={{ padding: '12px 16px', fontSize: '12px', textAlign: 'right' }}>Simulated Share</th>
                  <th style={{ padding: '12px 16px', fontSize: '12px', textAlign: 'center', width: '140px' }}>Actions</th>
                </tr>
              </thead>
              <tbody id="clickModalTableBody">
                {tasks.length === 0 ? (
                  <tr>
                    <td colSpan={3} style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '20px' }}>
                      System was idle at this point.
                    </td>
                  </tr>
                ) : (
                  tasks.map((t, idx) => {
                    const parts = t.split(' (');
                    const name = parts[0];
                    const speed = parts[1] ? parts[1].replace(')', '') : '0.0 KiB/s';
                    
                    return (
                      <tr key={idx}>
                        <td style={{ padding: '10px 16px', fontWeight: 600, color: 'white' }}>{idx + 1}. {name}</td>
                        <td style={{ padding: '10px 16px', textAlign: 'right', color: '#ffcc00', fontWeight: 600 }}>{speed}</td>
                        <td style={{ padding: '10px 16px', textAlign: 'center' }}>
                          <ProcessActions
                            processName={name}
                            path=""
                            fileHash=""
                            socketData={socketData}
                            logData={logData}
                            onOpenCopy={onOpenCopy}
                            onOpenSearch={onOpenSearch}
                            searchVirusTotal={searchVirusTotal}
                            alwaysVisible={true}
                          />
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
        <div style={{ display: 'flex', justifyContent: 'flex-end' }} className="modal-footer">
          <button className="filter-btn" style={{ background: 'var(--accent-color)', color: 'white', padding: '10px 24px' }} onClick={onClose}>Done</button>
        </div>
      </div>
    </div>
  );
}
