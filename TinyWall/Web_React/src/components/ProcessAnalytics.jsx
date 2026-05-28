import React, { useEffect, useRef, useState } from 'react';

export default function ProcessAnalytics({
  analyticsPoints,
  socketData,
  logData,
  formatSpeed
}) {
  const canvasRef = useRef(null);
  const [selectedProcess, setSelectedProcess] = useState('');
  const [processesList, setProcessesList] = useState([]);
  const [activeConnsCount, setActiveConnsCount] = useState(0);
  const [peakSpeed, setPeakSpeed] = useState(0);
  const [totalEvents, setTotalEvents] = useState(0);

  // Initialize processes dropdown targets
  useEffect(() => {
    const processes = new Set();
    analyticsPoints.forEach((p) => {
      if (p.PeakTask && p.PeakTask !== 'Idle') {
        p.PeakTask.split(';').forEach((t) => {
          const name = t.split(' (')[0].trim();
          if (name && name !== 'System Service') {
            processes.add(name);
          }
        });
      }
    });

    socketData.forEach((s) => {
      if (s.ProcessName && s.ProcessName !== 'System / Services') {
        processes.add(s.ProcessName);
      }
    });

    const sorted = Array.from(processes).sort();
    setProcessesList(sorted);

    if (sorted.length > 0 && !selectedProcess) {
      setSelectedProcess(sorted[0]);
    }
  }, [analyticsPoints, socketData]);

  // Update dynamic stats and draw chart when target changes
  useEffect(() => {
    if (!selectedProcess) return;

    // 1. Active Connections
    const conns = socketData.filter((s) => s.ProcessName === selectedProcess).length;
    setActiveConnsCount(conns);

    // 2. Peak Speed
    let peak = 0;
    analyticsPoints.forEach((p) => {
      if (p.PeakTask && p.PeakTask.includes(selectedProcess)) {
        p.PeakTask.split(';').forEach((t) => {
          if (t.startsWith(selectedProcess)) {
            const speedPart = t.split('(')[1]?.replace(')', '').trim();
            if (speedPart) {
              let bytes = parseFloat(speedPart);
              if (speedPart.includes('MiB/s')) bytes *= 1024 * 1024;
              else if (speedPart.includes('KiB/s')) bytes *= 1024;
              if (bytes > peak) peak = bytes;
            }
          }
        });
      }
    });
    setPeakSpeed(peak);

    // 3. Events Logged
    const events = logData.filter((l) => l.ProcessName === selectedProcess).length;
    setTotalEvents(events);

    // 4. Draw timeline history chart
    drawTimeline();
  }, [selectedProcess, analyticsPoints, socketData, logData]);

  // Handle Resize
  useEffect(() => {
    const handleResize = () => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      canvas.width = canvas.parentElement.getBoundingClientRect().width;
      canvas.height = 300;
      drawTimeline();
    };

    window.addEventListener('resize', handleResize);
    handleResize();

    return () => {
      window.removeEventListener('resize', handleResize);
    };
  }, [selectedProcess, analyticsPoints]);

  const drawTimeline = () => {
    const canvas = canvasRef.current;
    if (!canvas || !selectedProcess) return;
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
      ctx.fillText("Select an application from the dropdown to view its history.", w / 2, h / 2);
      return;
    }

    // Parse speeds at each timestamp
    const values = points.map((p) => {
      let speedBytes = 0;
      if (p.PeakTask && p.PeakTask.includes(selectedProcess)) {
        p.PeakTask.split(';').forEach((t) => {
          if (t.startsWith(selectedProcess)) {
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

    // Draw grid lines
    ctx.strokeStyle = 'rgba(255,255,255,0.03)';
    ctx.lineWidth = 1;
    for (let i = 1; i < 5; i++) {
      const y = (h / 5) * i;
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(w, y);
      ctx.stroke();
    }

    const getX = (index) => {
      if (points.length <= 1) return w / 2;
      return (w / (points.length - 1)) * index;
    };

    const getY = (value) => {
      return h - (h - 60) * (value / maxVal) - 30;
    };

    // Draw magenta line
    ctx.beginPath();
    ctx.moveTo(getX(0), getY(values[0]));
    for (let i = 1; i < points.length; i++) {
      ctx.lineTo(getX(i), getY(values[i]));
    }

    ctx.strokeStyle = 'var(--magenta-color)';
    ctx.lineWidth = 2.5;
    ctx.shadowBlur = 12;
    ctx.shadowColor = 'var(--magenta-glow)';
    ctx.stroke();
    ctx.shadowBlur = 0;

    // Fill area
    ctx.lineTo(getX(points.length - 1), h);
    ctx.lineTo(getX(0), h);
    ctx.closePath();
    ctx.fillStyle = 'rgba(255, 0, 127, 0.12)';
    ctx.fill();

    // Labels
    if (points.length >= 2) {
      ctx.font = '10px Outfit';
      ctx.fillStyle = 'var(--text-secondary)';

      const formatLabel = (timeStr) => {
        const d = new Date(timeStr);
        return `${d.toLocaleDateString([], { month: 'short', day: 'numeric' })} ${d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
      };

      ctx.textAlign = 'left';
      ctx.fillText(formatLabel(points[0].Time), 20, h - 10);
      ctx.textAlign = 'right';
      ctx.fillText(formatLabel(points[points.length - 1].Time), w - 20, h - 10);
    }

    ctx.font = '13px Outfit';
    ctx.textAlign = 'left';
    ctx.fillStyle = 'var(--magenta-color)';
    ctx.fillText(`Selected App: ${selectedProcess} (Peak: ${formatSpeed(Math.max(...values) * 1024)})`, 20, 30);
  };

  return (
    <div className="grid-2col">
      <div className="panel">
        <div className="panel-header">
          <div className="panel-title">Select Target Process</div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            <span style={{ fontSize: '13px', color: 'var(--text-secondary)', fontWeight: 600 }}>Choose Application:</span>
            <select
              className="search-input"
              value={selectedProcess}
              onChange={(e) => setSelectedProcess(e.target.value)}
              style={{ padding: '10px 14px', cursor: 'pointer', width: '100%' }}
            >
              {processesList.length === 0 ? (
                <option value="">No active network tasks found</option>
              ) : (
                processesList.map((p, idx) => (
                  <option key={idx} value={p}>{p}</option>
                ))
              )}
            </select>
          </div>
          
          <div style={{ background: 'rgba(255,255,255,0.03)', border: '1px solid var(--border-color)', padding: '18px', borderRadius: 'var(--border-radius)', display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '10px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>Active Connections:</span>
              <span style={{ fontWeight: 600, color: 'white', fontSize: '14px' }}>{activeConnsCount} conns</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>Peak Hist. Bandwidth:</span>
              <span style={{ fontWeight: 600, color: '#ffcc00', fontSize: '14px' }}>{formatSpeed(peakSpeed)}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ color: 'var(--text-secondary)', fontSize: '13px' }}>Total Events Logged:</span>
              <span style={{ fontWeight: 600, color: 'var(--danger-color)', fontSize: '14px' }}>{totalEvents} occurrences</span>
            </div>
          </div>
        </div>
      </div>

      <div className="panel">
        <div className="panel-header">
          <div className="panel-title">Process Throughput Timeline</div>
        </div>
        <div className="chart-container">
          <canvas ref={canvasRef} />
        </div>
      </div>
    </div>
  );
}
