import React, { useEffect, useRef, useState } from 'react';
import { Calendar, ShieldAlert } from 'lucide-react';
import ProcessActions from './ProcessActions';

export default function BandwidthChart({
  analyticsPoints,
  socketData,
  logData,
  onOpenCopy,
  onOpenSearch,
  searchVirusTotal,
  formatSpeed,
  detailedTooltipCheck,
  setDetailedTooltipCheck,
  lookbackNum,
  setLookbackNum,
  lookbackUnit,
  setLookbackUnit,
  customRangeActive,
  setCustomRangeActive,
  customDateStart,
  setCustomDateStart,
  customDateEnd,
  setCustomDateEnd,
  customTimeStart,
  setCustomTimeStart,
  customTimeEnd,
  setCustomTimeEnd,
  onApplyCustomRange,
  onOpenClickModal,
  rxSpeed,
  txSpeed
}) {
  const canvasRef = useRef(null);
  const [hoverIndex, setHoverIndex] = useState(-1);
  const [tooltipData, setTooltipData] = useState(null);

  // Resize and redraw canvas
  useEffect(() => {
    const handleResize = () => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const rect = canvas.parentElement.getBoundingClientRect();
      canvas.width = rect.width;
      canvas.height = 300;
      drawChart();
    };

    window.addEventListener('resize', handleResize);
    handleResize();

    return () => {
      window.removeEventListener('resize', handleResize);
    };
  }, [analyticsPoints, hoverIndex]);

  // Redraw when points or hover state changes
  useEffect(() => {
    drawChart();
  }, [analyticsPoints, hoverIndex, detailedTooltipCheck]);

  const drawChart = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const w = canvas.width;
    const h = canvas.height;
    ctx.clearRect(0, 0, w, h);

    const points = analyticsPoints || [];
    if (points.length === 0) {
      ctx.font = '14px Outfit';
      ctx.fillStyle = 'var(--text-secondary)';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
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

    const rxValues = points.map(p => p.Rx / 1024);
    const txValues = points.map(p => p.Tx / 1024);
    const maxVal = Math.max(...rxValues, ...txValues, 10); // Minimum scale of 10 KB/s

    const getX = (index) => {
      if (points.length <= 1) return w / 2;
      return (w / (points.length - 1)) * index;
    };

    const getY = (value) => {
      return h - (h - 60) * (value / maxVal) - 30;
    };

    // Draw Rx
    drawPath(rxValues, 'rgba(0, 255, 204, 0.15)', 'rgba(0, 255, 204, 1)', 2);

    // Draw Tx
    drawPath(txValues, 'rgba(138, 43, 226, 0.15)', 'rgba(138, 43, 226, 1)', 2);

    function drawPath(history, fillGradient, strokeColor, lineWidth) {
      if (points.length === 0) return;
      ctx.beginPath();
      ctx.moveTo(getX(0), getY(history[0]));

      for (let i = 1; i < points.length; i++) {
        ctx.lineTo(getX(i), getY(history[i]));
      }

      ctx.strokeStyle = strokeColor;
      ctx.lineWidth = lineWidth;
      ctx.shadowBlur = 10;
      ctx.shadowColor = strokeColor;
      ctx.stroke();
      ctx.shadowBlur = 0; // reset

      ctx.lineTo(getX(points.length - 1), h);
      ctx.lineTo(getX(0), h);
      ctx.closePath();
      ctx.fillStyle = fillGradient;
      ctx.fill();
    }

    // Highlight vertical line and dots on hover
    if (hoverIndex >= 0 && hoverIndex < points.length) {
      const hx = getX(hoverIndex);
      ctx.beginPath();
      ctx.setLineDash([5, 5]);
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
      ctx.moveTo(hx, 0);
      ctx.lineTo(hx, h);
      ctx.stroke();
      ctx.setLineDash([]); // Reset

      const activeRxY = getY(rxValues[hoverIndex]);
      const activeTxY = getY(txValues[hoverIndex]);

      // Rx Dot
      ctx.beginPath();
      ctx.arc(hx, activeRxY, 6, 0, 2 * Math.PI);
      ctx.fillStyle = '#00ffcc';
      ctx.shadowBlur = 15;
      ctx.shadowColor = '#00ffcc';
      ctx.fill();
      ctx.lineWidth = 2;
      ctx.strokeStyle = '#ffffff';
      ctx.stroke();

      // Tx Dot
      ctx.beginPath();
      ctx.arc(hx, activeTxY, 6, 0, 2 * Math.PI);
      ctx.fillStyle = '#8a2be2';
      ctx.shadowBlur = 15;
      ctx.shadowColor = '#8a2be2';
      ctx.fill();
      ctx.stroke();

      ctx.shadowBlur = 0; // reset
    }

    // Draw legends
    ctx.font = '12px Outfit';
    ctx.textAlign = 'left';
    ctx.fillStyle = '#00ffcc';
    ctx.fillText(`Down: ${formatSpeed(rxSpeed)}`, 20, 30);
    ctx.fillStyle = '#8a2be2';
    ctx.fillText(`Up: ${formatSpeed(txSpeed)}`, 20, 50);

    // Draw bottom timeline
    if (points.length >= 2) {
      ctx.font = '10px Outfit';
      ctx.fillStyle = 'var(--text-secondary)';

      const formatLabel = (timeStr) => {
        const d = new Date(timeStr);
        const time = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const date = d.toLocaleDateString([], { month: 'short', day: 'numeric' });
        return `${date} ${time}`;
      };

      ctx.textAlign = 'left';
      ctx.fillText(formatLabel(points[0].Time), 20, h - 10);

      ctx.textAlign = 'right';
      ctx.fillText(formatLabel(points[points.length - 1].Time), w - 20, h - 10);

      if (points.length >= 3) {
        ctx.textAlign = 'center';
        const mid = Math.floor(points.length / 2);
        ctx.fillText(formatLabel(points[mid].Time), w / 2, h - 10);
      }
    }
  };

  const handleMouseMove = (e) => {
    const canvas = canvasRef.current;
    if (!canvas || !analyticsPoints || analyticsPoints.length === 0) return;
    const rect = canvas.getBoundingClientRect();
    const mouseX = e.clientX - rect.left;
    const w = canvas.width;
    const idx = Math.round((mouseX / w) * (analyticsPoints.length - 1));

    if (idx >= 0 && idx < analyticsPoints.length) {
      setHoverIndex(idx);
      const point = analyticsPoints[idx];
      setTooltipData({
        clientX: e.clientX,
        clientY: e.clientY,
        point
      });
    }
  };

  const handleMouseLeave = () => {
    setHoverIndex(-1);
    setTooltipData(null);
  };

  const handleCanvasClick = () => {
    if (hoverIndex >= 0 && analyticsPoints && analyticsPoints[hoverIndex]) {
      onOpenClickModal(analyticsPoints[hoverIndex]);
    }
  };

  // Compile Top Bandwidth Process items
  const processCounts = {};
  socketData.forEach((s) => {
    if (!processCounts[s.ProcessName]) {
      processCounts[s.ProcessName] = { name: s.ProcessName, count: 0, path: s.Path, fileHash: s.FileHash };
    }
    processCounts[s.ProcessName].count++;
  });

  const sortedProcesses = Object.values(processCounts).sort((a, b) => b.count - a.count);
  const maxCount = sortedProcesses.length > 0 ? Math.max(...sortedProcesses.map(p => p.count)) : 1;

  return (
    <div className="grid-2col">
      <div className="panel">
        <div className="panel-header" style={{ flexWrap: 'wrap', gap: '15px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '15px' }}>
            <div className="panel-title">Network Throughput History</div>
            
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <label className="switch-container">
                <input
                  type="checkbox"
                  checked={detailedTooltipCheck}
                  onChange={(e) => setDetailedTooltipCheck(e.target.checked)}
                />
                <span className="switch-slider"></span>
              </label>
              <span style={{ fontSize: '12px', color: 'var(--text-secondary)', userSelect: 'none', fontWeight: 500 }}>
                Detailed View
              </span>
            </div>
          </div>

          <div className="controls-box" style={{ gap: '15px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', background: 'var(--bg-color)', padding: '6px 12px', borderRadius: 'var(--border-radius)', border: '1px solid var(--border-color)', flexWrap: 'wrap' }}>
              <span style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)' }}>Recent Activity:</span>
              
              <div className="filter-btn-group" style={{ border: 'none', padding: 0, background: 'transparent', gap: '4px' }}>
                {[1, 5, 10, 30].map(n => (
                  <button
                    key={n}
                    className={`filter-btn ${lookbackNum === n && !customRangeActive ? 'active' : ''}`}
                    style={{ padding: '6px 12px' }}
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
                style={{ width: '70px', minWidth: '70px', padding: '6px 10px', textAlign: 'center' }}
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
                    style={{ padding: '6px 12px' }}
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
              style={{ padding: '10px 18px', border: '1px solid var(--border-color)', display: 'inline-flex', alignItems: 'center', gap: '6px' }}
              onClick={() => setCustomRangeActive(!customRangeActive)}
            >
              <Calendar size={14} /> Custom Range
            </button>
          </div>
        </div>

        {customRangeActive && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '15px', background: 'rgba(22, 22, 34, 0.6)', padding: '12px 20px', borderRadius: 'var(--border-radius)', border: '1px solid var(--border-color)', flexWrap: 'wrap', marginTop: '-10px' }}>
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
            <button className="filter-btn" style={{ padding: '8px 20px', background: 'var(--accent-color)', color: 'white', borderRadius: '8px' }} onClick={onApplyCustomRange}>
              Apply Range
            </button>
          </div>
        )}

        <div className="chart-container">
          <canvas
            ref={canvasRef}
            onMouseMove={handleMouseMove}
            onMouseLeave={handleMouseLeave}
            onClick={handleCanvasClick}
            style={{ cursor: 'crosshair' }}
          />
        </div>

        {/* Dynamic Tooltip */}
        {tooltipData && (
          <div
            id="chartTooltip"
            style={{
              opacity: 1,
              left: `${tooltipData.clientX + window.scrollX}px`,
              top: `${tooltipData.clientY + window.scrollY}px`,
              pointerEvents: 'none'
            }}
          >
            <div style={{ fontWeight: 600, color: 'var(--text-secondary)', fontSize: '11px', marginBottom: '4px' }}>
              {new Date(tooltipData.point.Time).toLocaleDateString()} {new Date(tooltipData.point.Time).toLocaleTimeString()}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '2px' }}>
              <span style={{ color: 'var(--success-color)', fontSize: '10px' }}>●</span>
              <span style={{ fontSize: '12px' }}>Down: <strong style={{ color: 'var(--success-color)' }}>{formatSpeed(tooltipData.point.Rx)}</strong></span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
              <span style={{ color: 'var(--accent-color)', fontSize: '10px' }}>●</span>
              <span style={{ fontSize: '12px' }}>Up: <strong style={{ color: 'var(--accent-color)' }}>{formatSpeed(tooltipData.point.Tx)}</strong></span>
            </div>
            <div style={{ marginTop: '4px', paddingTop: '6px', borderTop: '1px solid rgba(255,255,255,0.08)' }}>
              <div style={{ color: 'var(--text-secondary)', fontSize: '9px', textTransform: 'uppercase', letterSpacing: '0.5px', fontWeight: 600 }}>
                {detailedTooltipCheck ? 'Parsed Task Breakdown' : 'Heavy Peak Task'}
              </div>
              
              {detailedTooltipCheck && tooltipData.point.PeakTask && tooltipData.point.PeakTask.includes(';') ? (
                tooltipData.point.PeakTask.split(';').map((t, idx) => {
                  const parts = t.split(' (');
                  const name = parts[0];
                  const rawSpeed = parts[1] ? parts[1].replace(')', '') : '0.0 KiB/s';
                  
                  const match = rawSpeed.match(/(\d+)\s+connections?\s*-\s*(.+)/);
                  
                  let connText = '';
                  let rateText = rawSpeed;
                  
                  if (match) {
                    connText = `${match[1]}c`;
                    rateText = match[2];
                  }

                  const isDownloadDominant = (tooltipData.point.Rx || 0) > (tooltipData.point.Tx || 0);
                  const rateColor = isDownloadDominant ? 'var(--success-color)' : 'var(--accent-color)';

                  return (
                    <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '16px', marginTop: '5px', fontSize: '11px' }}>
                      <span style={{ color: 'white', fontWeight: 500, display: 'flex', alignItems: 'center', gap: '4px' }}>
                        <span style={{ color: 'var(--accent-color)', fontSize: '8px' }}>▶</span> {idx + 1}. {name}
                      </span>
                      <div style={{ display: 'flex', gap: '4px', alignItems: 'center', fontWeight: 600 }}>
                        {connText && <span style={{ color: '#ffcc00' }}>{connText}</span>}
                        {connText && <span style={{ color: 'var(--text-secondary)' }}>-</span>}
                        <span style={{ color: rateColor }}>{rateText}</span>
                      </div>
                    </div>
                  );
                })
              ) : (
                <div style={{ color: '#ffcc00', fontWeight: 600, fontSize: '12px', marginTop: '2px', display: 'flex', alignItems: 'center', gap: '4px' }}>
                  <span>⚡</span> <span>{tooltipData.point.PeakTask ? tooltipData.point.PeakTask.split(';')[0] : 'Idle'}</span>
                </div>
              )}
            </div>
          </div>
        )}

        {txSpeed > 5 * 1024 * 1024 && (
          <div className="alert-card" id="exfiltrationAlert" style={{ marginTop: '20px' }}>
            <ShieldAlert size={20} style={{ color: 'var(--danger-color)', flexShrink: 0 }} />
            <div>
              <strong>Exfiltration Guard Notice:</strong> High volumes of outbound data detected from non-verified background application to remote server. Verify digital signature or restrict socket ports immediately.
            </div>
          </div>
        )}
      </div>

      <div className="panel">
        <div className="panel-header">
          <div className="panel-title">Bandwidth by Process</div>
        </div>
        <div className="bandwidth-list">
          {sortedProcesses.length === 0 ? (
            <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '40px' }}>
              No active traffic data.
            </div>
          ) : (
            sortedProcesses.slice(0, 5).map((a, idx) => {
              const widthPercentage = (a.count / maxCount) * 100;
              return (
                <div key={idx} className="bandwidth-item">
                  <div className="bandwidth-header" style={{ alignItems: 'center' }}>
                    <span className="bandwidth-app" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      {a.name}
                      <ProcessActions
                        processName={a.name}
                        path={a.path}
                        fileHash={a.fileHash}
                        socketData={socketData}
                        logData={logData}
                        onOpenCopy={onOpenCopy}
                        onOpenSearch={onOpenSearch}
                        searchVirusTotal={searchVirusTotal}
                      />
                    </span>
                    <span style={{ color: 'var(--text-secondary)' }}>{a.count} active connections</span>
                  </div>
                  <div className="bandwidth-track">
                    <div className="bandwidth-bar" style={{ width: `${widthPercentage}%` }}></div>
                  </div>
                </div>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
}
