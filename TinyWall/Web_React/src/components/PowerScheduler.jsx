import React, { useState, useEffect, useRef } from 'react';
import { Power, Timer, Clock, ShieldAlert, Monitor, Volume2, HardDrive, Tv } from 'lucide-react';

export default function PowerScheduler({ showToast }) {
  const [schedulerState, setSchedulerState] = useState({
    isActive: false,
    action: 'shutdown',
    triggerType: 'duration',
    mode: 'smart',
    canCancel: true,
    targetTime: '',
    secondsRemaining: 0,
    requiresPassword: false,
    jellyfinStreaming: false
  });

  // Scheduling Form States
  const [selectedAction, setSelectedAction] = useState('shutdown');
  const [selectedTrigger, setSelectedTrigger] = useState('duration');
  const [selectedMode, setSelectedMode] = useState('smart');
  const [allowCancel, setAllowCancel] = useState(true);

  // Trigger Values
  const [hours, setHours] = useState(1);
  const [minutes, setMinutes] = useState(0);
  const [exactTime, setExactTime] = useState('23:00');
  const [exactDate, setExactDate] = useState(() => {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  });
  const [nextOption, setNextOption] = useState(true);
  const [now, setNow] = useState(new Date());
  const [idleMinutes, setIdleMinutes] = useState(15);
  const [downloadSpeed, setDownloadSpeed] = useState(100); // in KB/s
  const [jellyfinPort, setJellyfinPort] = useState(8096);
  const [graceMinutes, setGraceMinutes] = useState(5);

  // Chained / Combined Trigger States
  const [chainActive, setChainActive] = useState(false);
  const [selectedChainTrigger, setSelectedChainTrigger] = useState('jellyfin');
  const [chainHours, setChainHours] = useState(0);
  const [chainMinutes, setChainMinutes] = useState(30);
  const [chainExactTime, setChainExactTime] = useState('23:30');
  const [chainExactDate, setChainExactDate] = useState(() => {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
  });
  const [chainNextOption, setChainNextOption] = useState(true);
  const [chainIdleMinutes, setChainIdleMinutes] = useState(15);
  const [chainDownloadSpeed, setChainDownloadSpeed] = useState(100);
  const [chainJellyfinPort, setChainJellyfinPort] = useState(8096);

  // Cancellation State
  const [cancelPassword, setCancelPassword] = useState('');
  const [showPasswordPrompt, setShowPasswordPrompt] = useState(false);
  const [cancelError, setCancelError] = useState('');

  // Keep 'now' updated every second for live date/time & duration previews
  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(t);
  }, []);

  // Audio Context Ref
  const audioContextRef = useRef(null);
  const playBeepRef = useRef(false);

  // Fetch status on mount and poll every 1 second
  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const res = await fetch('/api/power/status');
        if (res.ok) {
          const status = await res.json();
          setSchedulerState(status);

          // Alert warning chimes in the final 60 seconds
          if (status.isActive && status.secondsRemaining > 0 && status.secondsRemaining <= 60) {
            // Play a brief warning sound every 10 seconds, and every second in the last 10 seconds
            const rem = status.secondsRemaining;
            if (rem % 10 === 0 || rem <= 10) {
              triggerBeep();
            }
          }
        }
      } catch (err) {
        console.error("Failed to fetch power status:", err);
      }
    };

    fetchStatus();
    const timer = setInterval(fetchStatus, 1000);
    return () => clearInterval(timer);
  }, []);

  // Web Audio Synth Warning Beep
  const triggerBeep = () => {
    try {
      if (!audioContextRef.current) {
        audioContextRef.current = new (window.AudioContext || window.webkitAudioContext)();
      }
      const ctx = audioContextRef.current;
      if (ctx.state === 'suspended') {
        ctx.resume();
      }

      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(660, ctx.currentTime); // Gentle clear warning frequency

      gain.gain.setValueAtTime(0.08, ctx.currentTime); // Low non-intrusive volume
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.15); // Quick decay

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start();
      osc.stop(ctx.currentTime + 0.25);
    } catch (e) {
      console.warn("Web Audio chimes blocked/unsupported:", e);
    }
  };

  const handleSchedule = async () => {
    try {
      let value = 0;
      if (selectedTrigger === 'duration') {
        value = (hours * 3600) + (minutes * 60);
        if (value <= 0) {
          alert("Please select a duration greater than 0 minutes.");
          return;
        }
      } else if (selectedTrigger === 'idle') {
        value = idleMinutes;
      } else if (selectedTrigger === 'download') {
        value = downloadSpeed;
      } else if (selectedTrigger === 'jellyfin') {
        // Uses default or custom Jellyfin port checks
        value = jellyfinPort;
      }

      const params = new URLSearchParams({
        action: selectedAction,
        trigger: selectedTrigger,
        value: value.toString(),
        mode: selectedMode,
        canCancel: allowCancel.toString(),
        exactTime: selectedTrigger === 'exact' ? getExactTimeISO(exactTime, exactDate, nextOption) : '',
        graceSeconds: (graceMinutes * 60).toString()
      });

      if (chainActive) {
        let chainVal = 0;
        if (selectedChainTrigger === 'duration') {
          chainVal = (chainHours * 3600) + (chainMinutes * 60);
          if (chainVal <= 0) {
            alert("Please select a chained duration greater than 0 minutes.");
            return;
          }
        } else if (selectedChainTrigger === 'idle') {
          chainVal = chainIdleMinutes;
        } else if (selectedChainTrigger === 'download') {
          chainVal = chainDownloadSpeed;
        } else if (selectedChainTrigger === 'jellyfin') {
          chainVal = chainJellyfinPort;
        }

        params.append('chainTrigger', selectedChainTrigger);
        params.append('chainValue', chainVal.toString());
        if (selectedChainTrigger === 'exact') {
          params.append('chainExactTime', getExactTimeISO(chainExactTime, chainExactDate, chainNextOption));
        }
      }

      const res = await fetch(`/api/power/schedule?${params.toString()}`, { method: 'POST' });
      const data = await res.json();
      if (data.success) {
        showToast("Power schedule initiated successfully!");
        setCancelPassword('');
        setCancelError('');
        setShowPasswordPrompt(false);
      } else {
        alert(`Error: ${data.error}`);
      }
    } catch (err) {
      alert("Failed to send schedule request.");
    }
  };

  const handleCancel = async () => {
    try {
      const params = new URLSearchParams();
      if (schedulerState.requiresPassword) {
        if (!cancelPassword) {
          setShowPasswordPrompt(true);
          return;
        }
        params.append('password', cancelPassword);
      }

      const res = await fetch(`/api/power/cancel?${params.toString()}`, { method: 'POST' });
      const data = await res.json();
      if (data.success) {
        showToast("Schedule cancelled successfully.");
        setShowPasswordPrompt(false);
        setCancelPassword('');
        setCancelError('');
      } else {
        setCancelError(data.error || "Incorrect password.");
      }
    } catch (err) {
      alert("Failed to send cancel request.");
    }
  };

  const getExactTimeISO = (timeStr, dateStr, useNext) => {
    const [h, m] = timeStr.split(':').map(Number);
    const d = new Date();
    if (useNext) {
      d.setHours(h, m, 0, 0);
      if (d <= new Date()) {
        d.setDate(d.getDate() + 1);
      }
      return d.toISOString();
    } else {
      const [yr, mo, dy] = dateStr.split('-').map(Number);
      d.setFullYear(yr, mo - 1, dy);
      d.setHours(h, m, 0, 0);
      return d.toISOString();
    }
  };

  const formatSeconds = (totalSec) => {
    const hrs = Math.floor(totalSec / 3600);
    const mins = Math.floor((totalSec % 3600) / 60);
    const secs = totalSec % 60;
    
    const pad = (num) => String(num).padStart(2, '0');
    if (hrs > 0) {
      return `${hrs}h ${pad(mins)}m ${pad(secs)}s`;
    }
    return `${mins}m ${pad(secs)}s`;
  };

  // Color theme helpers based on action type
  const getActionTheme = (action) => {
    return action === 'shutdown' 
      ? { color: 'var(--danger-color)', glow: 'var(--danger-glow)', bg: 'rgba(255, 51, 102, 0.1)' }
      : action === 'restart'
      ? { color: '#ff9800', glow: 'rgba(255, 152, 0, 0.4)', bg: 'rgba(255, 152, 0, 0.1)' }
      : action === 'sleep'
      ? { color: '#7e57c2', glow: 'rgba(126, 87, 194, 0.4)', bg: 'rgba(126, 87, 194, 0.1)' }
      : { color: '#00bcd4', glow: 'rgba(0, 188, 212, 0.4)', bg: 'rgba(0, 188, 212, 0.1)' };
  };

  const activeTheme = getActionTheme(schedulerState.action);
  const currentActionTheme = getActionTheme(selectedAction);

  // Check if countdown warning is active
  const isWarningActive = schedulerState.isActive && schedulerState.secondsRemaining <= 60;

  return (
    <div className="panel" style={{ padding: '0' }}>
      
      {/* 60-Second Warning Alert Bar */}
      {isWarningActive && (
        <div style={{
          background: 'var(--danger-color)',
          color: 'white',
          padding: '12px',
          fontWeight: 'bold',
          textAlign: 'center',
          animation: 'blinker 1s linear infinite',
          fontSize: '15px',
          letterSpacing: '1px',
          borderRadius: '12px 12px 0 0',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: '8px'
        }}>
          <Volume2 size={18} />
          SYSTEM {schedulerState.action.toUpperCase()} IMMINENT: {formatSeconds(schedulerState.secondsRemaining)} REMAINING!
        </div>
      )}

      <div style={{ padding: '24px' }}>
        
        {/* Active Schedule Overview */}
        {schedulerState.isActive ? (
          <div style={{
            background: 'var(--surface-color)',
            border: `1px solid ${isWarningActive ? 'var(--danger-color)' : 'var(--border-color)'}`,
            borderRadius: '12px',
            padding: '30px',
            marginBottom: '24px',
            textAlign: 'center',
            boxShadow: isWarningActive ? '0 0 20px var(--danger-glow)' : 'none',
            transition: 'all 0.3s ease'
          }}>
            <div style={{ display: 'inline-flex', padding: '16px', borderRadius: '50%', background: activeTheme.bg, color: activeTheme.color, marginBottom: '16px', boxShadow: `0 0 15px ${activeTheme.glow}` }}>
              <Power size={32} />
            </div>

            <h2 style={{ fontSize: '22px', fontWeight: 'bold', marginBottom: '8px', textTransform: 'capitalize' }}>
              Scheduled {schedulerState.action} Active
            </h2>

            <p style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '24px' }}>
              Trigger Type: <strong style={{ textTransform: 'capitalize', color: 'white' }}>{schedulerState.triggerType === 'jellyfin' ? 'Jellyfin Stream Tracker' : schedulerState.triggerType}</strong> | Policy: <strong style={{ textTransform: 'capitalize', color: 'white' }}>{schedulerState.mode === 'smart' ? 'Smart Hybrid (5m grace)' : schedulerState.mode}</strong>
            </p>

            {/* Countdown / Value Render */}
            <div style={{
              fontSize: '48px',
              fontFamily: 'monospace',
              fontWeight: 'bold',
              color: activeTheme.color,
              textShadow: `0 0 10px ${activeTheme.glow}`,
              margin: '16px 0'
            }}>
              {schedulerState.triggerType === 'jellyfin' && !schedulerState.jellyfinStreaming && schedulerState.secondsRemaining === 300 ? (
                <div style={{ fontSize: '20px', color: 'var(--text-secondary)' }}>Waiting for first Jellyfin connection...</div>
              ) : schedulerState.jellyfinStreaming ? (
                <div style={{ fontSize: '22px', color: '#00ffcc', animation: 'pulse 1.5s infinite' }}>
                  📺 Active Jellyfin Stream Detected - Shutdown Paused
                </div>
              ) : (
                formatSeconds(schedulerState.secondsRemaining)
              )}
            </div>

            <p style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '30px' }}>
              Target Execution Local Time: <strong>{schedulerState.targetTime}</strong>
            </p>

            {schedulerState.hasChainTrigger && (
              <div style={{
                background: 'rgba(0, 188, 212, 0.05)',
                border: '1px solid rgba(0, 188, 212, 0.2)',
                borderRadius: '8px',
                padding: '10px 14px',
                fontSize: '12px',
                color: '#00bcd4',
                maxWidth: '450px',
                margin: '0 auto 20px auto',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '6px'
              }}>
                <span>🔗 <strong>Combination Chained Trigger Active:</strong> Satisfying current trigger will transition to <strong>{schedulerState.chainTrigger === 'jellyfin' ? 'Jellyfin Watch' : schedulerState.chainTrigger === 'exact' ? 'Exact Time' : schedulerState.chainTrigger === 'duration' ? 'Countdown' : schedulerState.chainTrigger}</strong> trigger next.</span>
              </div>
            )}

            {/* Cancel Actions */}
            {schedulerState.canCancel ? (
              <div style={{ maxWidth: '360px', margin: '0 auto' }}>
                {!showPasswordPrompt ? (
                  <button 
                    className="filter-btn active"
                    style={{
                      background: 'var(--danger-color)',
                      borderColor: 'var(--danger-color)',
                      color: 'white',
                      width: '100%',
                      padding: '12px',
                      borderRadius: '8px',
                      fontSize: '14px',
                      fontWeight: 'bold'
                    }}
                    onClick={handleCancel}
                  >
                    Cancel Scheduled Action
                  </button>
                ) : (
                  <div style={{ background: '#1a1a1a', padding: '16px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                    <div style={{ fontSize: '13px', marginBottom: '8px', color: 'var(--text-secondary)', textAlign: 'left' }}>
                      🔑 Enter FoxWall settings password to unlock cancellation:
                    </div>
                    <input 
                      type="password"
                      className="search-input"
                      style={{ width: '100%', marginBottom: '12px', boxSizing: 'border-box' }}
                      value={cancelPassword}
                      onChange={(e) => setCancelPassword(e.target.value)}
                      placeholder="Security Password..."
                    />
                    {cancelError && <div style={{ color: 'var(--danger-color)', fontSize: '12px', marginBottom: '8px', textAlign: 'left' }}>{cancelError}</div>}
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <button 
                        className="filter-btn active" 
                        style={{ flex: 1, background: 'var(--danger-color)', borderColor: 'var(--danger-color)' }}
                        onClick={handleCancel}
                      >
                        Confirm Cancel
                      </button>
                      <button 
                        className="filter-btn" 
                        style={{ flex: 1 }}
                        onClick={() => { setShowPasswordPrompt(false); setCancelPassword(''); setCancelError(''); }}
                      >
                        Back
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ) : (
              <div style={{
                color: 'var(--danger-color)',
                fontSize: '14px',
                fontWeight: 'bold',
                padding: '12px',
                background: 'rgba(255, 51, 102, 0.1)',
                border: '1px solid rgba(255, 51, 102, 0.3)',
                borderRadius: '8px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px'
              }}>
                <ShieldAlert size={16} />
                ⚠️ Force schedule active. This action cannot be aborted.
              </div>
            )}
          </div>
        ) : (
          
          /* Scheduling Setup View */
          <div>
            <h2 style={{ fontSize: '20px', fontWeight: 'bold', marginBottom: '24px', display: 'flex', alignItems: 'center', gap: '10px' }}>
              <Timer style={{ color: 'var(--accent-color)' }} /> PC Power Scheduler Setup
            </h2>

            {/* Step 1: Select Action */}
            <div style={{ marginBottom: '24px' }}>
              <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '10px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                1. Select Power Action
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '12px' }}>
                {[
                  { id: 'shutdown', name: 'Shutdown', icon: Power, desc: 'Complete turn off' },
                  { id: 'restart', name: 'Restart', icon: Timer, desc: 'Reboot computer' },
                  { id: 'sleep', name: 'Sleep', icon: Monitor, desc: 'Suspend system' },
                  { id: 'lock', name: 'Lock', icon: ShieldAlert, desc: 'Lock user session' }
                ].map((act) => {
                  const isSel = selectedAction === act.id;
                  const theme = getActionTheme(act.id);
                  return (
                    <div 
                      key={act.id}
                      onClick={() => setSelectedAction(act.id)}
                      style={{
                        background: 'var(--surface-color)',
                        border: `1px solid ${isSel ? theme.color : 'var(--border-color)'}`,
                        boxShadow: isSel ? `0 0 10px ${theme.glow}` : 'none',
                        borderRadius: '8px',
                        padding: '16px',
                        cursor: 'pointer',
                        textAlign: 'center',
                        transition: 'all 0.2s ease',
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center'
                      }}
                    >
                      <act.icon size={22} style={{ color: isSel ? theme.color : 'var(--text-secondary)', marginBottom: '8px' }} />
                      <div style={{ fontWeight: 'bold', fontSize: '14px', marginBottom: '2px', color: isSel ? 'white' : 'var(--text-secondary)' }}>{act.name}</div>
                      <div style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>{act.desc}</div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Step 2: Select Trigger */}
            <div style={{ marginBottom: '24px' }}>
              <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '10px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                2. Choose Execution Trigger
              </label>
              <div className="filter-btn-group" style={{ display: 'inline-flex', flexWrap: 'wrap', gap: '4px', background: '#121212', padding: '4px', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                {[
                  { id: 'duration', name: 'Countdown', icon: Timer },
                  { id: 'exact', name: 'Exact Time', icon: Clock },
                  { id: 'idle', name: 'Idle Guard', icon: Monitor },
                  { id: 'download', name: 'Download Target', icon: HardDrive },
                  { id: 'jellyfin', name: 'Jellyfin Watch', icon: Tv }
                ].map((trig) => (
                  <button
                    key={trig.id}
                    className={`filter-btn ${selectedTrigger === trig.id ? 'active' : ''}`}
                    onClick={() => setSelectedTrigger(trig.id)}
                    style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', padding: '8px 12px', height: 'auto', borderRadius: '6px' }}
                  >
                    <trig.icon size={13} />
                    {trig.name}
                  </button>
                ))}
              </div>
            </div>

            {/* Dynamic Trigger Customizer View */}
            <div style={{
              background: '#161616',
              border: '1px solid var(--border-color)',
              borderRadius: '8px',
              padding: '20px',
              marginBottom: '24px'
            }}>
              {selectedTrigger === 'duration' && (() => {
                const totalSeconds = (hours * 3600) + (minutes * 60);
                const targetD = new Date(now.getTime() + totalSeconds * 1000);
                const formattedTimeStr = targetD.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
                const formattedDateStr = targetD.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });

                return (
                  <div>
                    <div style={{ display: 'flex', gap: '20px', marginBottom: '12px' }}>
                      <div style={{ flex: 1 }}>
                        <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '6px' }}>Hours</label>
                        <input 
                          type="range" 
                          min="0" 
                          max="12" 
                          value={hours} 
                          onChange={(e) => setHours(parseInt(e.target.value))}
                          style={{ width: '100%', accentColor: currentActionTheme.color }}
                        />
                        <div style={{ textAlign: 'center', fontWeight: 'bold', marginTop: '6px' }}>{hours} hrs</div>
                      </div>
                      <div style={{ flex: 1 }}>
                        <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '6px' }}>Minutes</label>
                        <input 
                          type="range" 
                          min="0" 
                          max="59" 
                          value={minutes} 
                          onChange={(e) => setMinutes(parseInt(e.target.value))}
                          style={{ width: '100%', accentColor: currentActionTheme.color }}
                        />
                        <div style={{ textAlign: 'center', fontWeight: 'bold', marginTop: '6px' }}>{minutes} mins</div>
                      </div>
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center', lineHeight: '1.5' }}>
                      PC will {selectedAction} in <strong>{hours > 0 ? `${hours} hours and ` : ''}{minutes} minutes</strong><br />
                      at exactly <strong style={{ color: 'white' }}>{formattedTimeStr}</strong> on <strong style={{ color: 'white' }}>{formattedDateStr}</strong>
                    </div>
                  </div>
                );
              })()}

              {selectedTrigger === 'exact' && (() => {
                const [h, m] = exactTime.split(':').map(Number);
                let targetD = new Date(now);
                targetD.setHours(h, m, 0, 0);

                if (nextOption) {
                  if (targetD <= now) {
                    targetD.setDate(targetD.getDate() + 1);
                  }
                } else {
                  const [yr, mo, dy] = exactDate.split('-').map(Number);
                  targetD = new Date(yr, mo - 1, dy, h, m, 0, 0);
                }

                const diffMs = targetD - now;
                const isPast = diffMs < 0;
                
                let durationLeftStr = '';
                if (!isPast) {
                  const totalSec = Math.floor(diffMs / 1000);
                  const hrs = Math.floor(totalSec / 3600);
                  const mins = Math.floor((totalSec % 3600) / 60);
                  const secs = totalSec % 60;
                  const parts = [];
                  if (hrs > 0) parts.push(`${hrs} hr${hrs > 1 ? 's' : ''}`);
                  if (mins > 0) parts.push(`${mins} min${mins > 1 ? 's' : ''}`);
                  parts.push(`${secs} sec${secs > 1 ? 's' : ''}`);
                  durationLeftStr = parts.join(', ');
                }

                const formattedTimeStr = targetD.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                const formattedDateStr = targetD.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });

                return (
                  <div>
                    <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '8px', textAlign: 'center' }}>
                      Set Exact Execution Time (Local System Timezone)
                    </label>
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '12px' }}>
                      <div style={{ display: 'flex', gap: '12px', alignItems: 'center', justifyContent: 'center' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                          <span style={{ fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Time</span>
                          <input 
                            type="time" 
                            className="search-input" 
                            style={{ fontSize: '16px', padding: '8px 12px', width: '130px', textAlign: 'center' }}
                            value={exactTime}
                            onChange={(e) => setExactTime(e.target.value)}
                          />
                        </div>
                        
                        {!nextOption && (
                          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                            <span style={{ fontSize: '10px', color: 'var(--text-secondary)', marginBottom: '4px' }}>Date</span>
                            <input 
                              type="date" 
                              className="search-input" 
                              style={{ fontSize: '16px', padding: '8px 12px', width: '160px', textAlign: 'center' }}
                              value={exactDate}
                              onChange={(e) => setExactDate(e.target.value)}
                            />
                          </div>
                        )}
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', background: 'rgba(255,255,255,0.03)', padding: '6px 12px', borderRadius: '20px', border: '1px solid rgba(255,255,255,0.05)' }}>
                        <input 
                          type="checkbox" 
                          id="nextOption"
                          style={{ width: '15px', height: '15px', accentColor: 'var(--accent-color)', cursor: 'pointer' }}
                          checked={nextOption}
                          onChange={(e) => setNextOption(e.target.checked)}
                        />
                        <label htmlFor="nextOption" style={{ fontSize: '11px', cursor: 'pointer', userSelect: 'none', color: 'var(--text-secondary)' }}>
                          Next Occurrence (Auto Today/Tomorrow)
                        </label>
                      </div>
                    </div>

                    <div style={{ fontSize: '12px', color: isPast ? 'var(--danger-color)' : 'var(--text-secondary)', textAlign: 'center', marginTop: '16px', lineHeight: '1.5' }}>
                      {isPast ? (
                        <strong>⚠️ Selected execution time is in the past!</strong>
                      ) : (
                        <span>
                          PC will {selectedAction} in <strong style={{ color: currentActionTheme.color }}>{durationLeftStr}</strong><br />
                          at exactly <strong style={{ color: 'white' }}>{formattedTimeStr}</strong> on <strong style={{ color: 'white' }}>{formattedDateStr}</strong>
                        </span>
                      )}
                    </div>
                  </div>
                );
              })()}

              {selectedTrigger === 'idle' && (
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '8px' }}>
                    Idle Trigger Duration (Inactivity limit)
                  </label>
                  <input 
                    type="number" 
                    className="search-input" 
                    min="1" 
                    max="180" 
                    style={{ width: '100px', display: 'block', margin: '0 auto', textAlign: 'center' }}
                    value={idleMinutes}
                    onChange={(e) => setIdleMinutes(Math.max(1, parseInt(e.target.value) || 15))}
                  />
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center', marginTop: '10px' }}>
                    PC will automatically {selectedAction} once left completely idle for <strong>{idleMinutes} minutes</strong>
                  </div>
                </div>
              )}

              {selectedTrigger === 'download' && (
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '8px' }}>
                    Bandwidth speed limit threshold (KB/s)
                  </label>
                  <input 
                    type="number" 
                    className="search-input" 
                    min="1" 
                    style={{ width: '120px', display: 'block', margin: '0 auto', textAlign: 'center' }}
                    value={downloadSpeed}
                    onChange={(e) => setDownloadSpeed(Math.max(1, parseInt(e.target.value) || 100))}
                  />
                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center', marginTop: '10px' }}>
                    PC will {selectedAction} when active download rates drop below <strong>{downloadSpeed} KB/s</strong> for 3 consecutive minutes.
                  </div>
                </div>
              )}

              {selectedTrigger === 'jellyfin' && (
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '8px' }}>
                    Jellyfin Server Local Port
                  </label>
                  <input 
                    type="number" 
                    className="search-input" 
                    style={{ width: '120px', display: 'block', margin: '0 auto', textAlign: 'center' }}
                    value={jellyfinPort}
                    onChange={(e) => setJellyfinPort(Math.max(1, parseInt(e.target.value) || 8096))}
                  />
                  <div style={{ fontSize: '12px', color: '#00ffcc', textAlign: 'center', marginTop: '12px', fontWeight: 'bold' }}>
                    📺 Streaming Auto-Hold Guard Active
                  </div>
                  <div style={{ fontSize: '11px', color: 'var(--text-secondary)', textAlign: 'center', marginTop: '6px' }}>
                    The PC will monitor streaming activity on port {jellyfinPort} and will automatically {selectedAction} <strong>exactly 5 minutes after</strong> all streaming devices disconnect!
                  </div>
                </div>
              )}
            </div>

            {/* Chained / Combined Trigger Option */}
            <div style={{
              background: '#161616',
              border: '1px solid var(--border-color)',
              borderRadius: '8px',
              padding: '20px',
              marginBottom: '24px'
            }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <div>
                  <div style={{ fontWeight: 'bold', fontSize: '14px', color: 'white', display: 'flex', alignItems: 'center', gap: '8px' }}>
                    🔗 Chain Secondary Trigger (Combination Mode)
                  </div>
                  <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '2px' }}>
                    Satisfy the primary trigger, then transition to this second condition before executing
                  </div>
                </div>
                <input 
                  type="checkbox"
                  style={{ width: '18px', height: '18px', accentColor: 'var(--accent-color)', cursor: 'pointer' }}
                  checked={chainActive}
                  onChange={(e) => setChainActive(e.target.checked)}
                />
              </div>

              {chainActive && (
                <div style={{ marginTop: '16px', borderTop: '1px solid rgba(255,255,255,0.05)', paddingTop: '16px' }}>
                  <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '10px' }}>
                    Select Chained Trigger Type
                  </label>
                  <div className="filter-btn-group" style={{ display: 'inline-flex', flexWrap: 'wrap', gap: '4px', background: '#121212', padding: '4px', borderRadius: '8px', border: '1px solid var(--border-color)', marginBottom: '16px' }}>
                    {[
                      { id: 'duration', name: 'Countdown', icon: Timer },
                      { id: 'exact', name: 'Exact Time', icon: Clock },
                      { id: 'idle', name: 'Idle Guard', icon: Monitor },
                      { id: 'download', name: 'Download Target', icon: HardDrive },
                      { id: 'jellyfin', name: 'Jellyfin Watch', icon: Tv }
                    ].filter(t => t.id !== selectedTrigger).map((trig) => (
                      <button
                        key={trig.id}
                        type="button"
                        className={`filter-btn ${selectedChainTrigger === trig.id ? 'active' : ''}`}
                        onClick={() => {
                          setSelectedChainTrigger(trig.id);
                        }}
                        style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '11px', padding: '6px 10px', height: 'auto', borderRadius: '6px' }}
                      >
                        <trig.icon size={12} />
                        {trig.name}
                      </button>
                    ))}
                  </div>

                  {/* Chained Trigger Config Panel */}
                  <div style={{ background: '#0e0e0e', border: '1px solid rgba(255,255,255,0.03)', borderRadius: '6px', padding: '16px' }}>
                    {selectedChainTrigger === 'duration' && (() => {
                      return (
                        <div>
                          <div style={{ display: 'flex', gap: '20px', marginBottom: '12px' }}>
                            <div style={{ flex: 1 }}>
                              <label style={{ fontSize: '11px', color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Hours</label>
                              <input 
                                type="range" 
                                min="0" 
                                max="12" 
                                value={chainHours} 
                                onChange={(e) => setChainHours(parseInt(e.target.value))}
                                style={{ width: '100%', accentColor: currentActionTheme.color }}
                              />
                              <div style={{ textAlign: 'center', fontWeight: 'bold', fontSize: '12px', marginTop: '4px' }}>{chainHours} hrs</div>
                            </div>
                            <div style={{ flex: 1 }}>
                              <label style={{ fontSize: '11px', color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Minutes</label>
                              <input 
                                type="range" 
                                min="0" 
                                max="59" 
                                value={chainMinutes} 
                                onChange={(e) => setChainMinutes(parseInt(e.target.value))}
                                style={{ width: '100%', accentColor: currentActionTheme.color }}
                              />
                              <div style={{ textAlign: 'center', fontWeight: 'bold', fontSize: '12px', marginTop: '4px' }}>{chainMinutes} mins</div>
                            </div>
                          </div>
                          <div style={{ fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center' }}>
                            Then, countdown <strong>{chainHours > 0 ? `${chainHours} hours and ` : ''}{chainMinutes} minutes</strong>
                          </div>
                        </div>
                      );
                    })()}

                    {selectedChainTrigger === 'exact' && (() => {
                      const [h, m] = chainExactTime.split(':').map(Number);
                      let targetD = new Date(now);
                      targetD.setHours(h, m, 0, 0);

                      if (chainNextOption) {
                        if (targetD <= now) {
                          targetD.setDate(targetD.getDate() + 1);
                        }
                      } else {
                        const [yr, mo, dy] = chainExactDate.split('-').map(Number);
                        targetD = new Date(yr, mo - 1, dy, h, m, 0, 0);
                      }

                      const diffMs = targetD - now;
                      const isPast = diffMs < 0;

                      const formattedTimeStr = targetD.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                      const formattedDateStr = targetD.toLocaleDateString([], { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' });

                      return (
                        <div>
                          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '10px' }}>
                            <div style={{ display: 'flex', gap: '10px', alignItems: 'center', justifyContent: 'center' }}>
                              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                                <span style={{ fontSize: '9px', color: 'var(--text-secondary)', marginBottom: '2px' }}>Time</span>
                                <input 
                                  type="time" 
                                  className="search-input" 
                                  style={{ fontSize: '14px', padding: '6px 10px', width: '110px', textAlign: 'center' }}
                                  value={chainExactTime}
                                  onChange={(e) => setChainExactTime(e.target.value)}
                                />
                              </div>
                              
                              {!chainNextOption && (
                                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                                  <span style={{ fontSize: '9px', color: 'var(--text-secondary)', marginBottom: '2px' }}>Date</span>
                                  <input 
                                    type="date" 
                                    className="search-input" 
                                    style={{ fontSize: '14px', padding: '6px 10px', width: '140px', textAlign: 'center' }}
                                    value={chainExactDate}
                                    onChange={(e) => setChainExactDate(e.target.value)}
                                  />
                                </div>
                              )}
                            </div>

                            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', background: 'rgba(255,255,255,0.02)', padding: '4px 8px', borderRadius: '15px', border: '1px solid rgba(255,255,255,0.03)' }}>
                              <input 
                                type="checkbox" 
                                id="chainNextOption"
                                style={{ width: '13px', height: '13px', accentColor: 'var(--accent-color)', cursor: 'pointer' }}
                                checked={chainNextOption}
                                onChange={(e) => setChainNextOption(e.target.checked)}
                              />
                              <label htmlFor="chainNextOption" style={{ fontSize: '10px', cursor: 'pointer', userSelect: 'none', color: 'var(--text-secondary)' }}>
                                Next Occurrence (Auto Today/Tomorrow)
                              </label>
                            </div>
                          </div>

                          <div style={{ fontSize: '12px', color: isPast ? 'var(--danger-color)' : 'var(--text-secondary)', textAlign: 'center', marginTop: '12px', lineHeight: '1.4' }}>
                            {isPast ? (
                              <strong>⚠️ Selected chained time is in the past!</strong>
                            ) : (
                              <span>
                                Then, wait until exact time <strong style={{ color: 'white' }}>{formattedTimeStr}</strong> on <strong style={{ color: 'white' }}>{formattedDateStr}</strong>
                              </span>
                            )}
                          </div>
                        </div>
                      );
                    })()}

                    {selectedChainTrigger === 'idle' && (
                      <div>
                        <label style={{ fontSize: '11px', color: 'var(--text-secondary)', display: 'block', marginBottom: '6px', textAlign: 'center' }}>
                          Chained Idle Duration (Inactivity limit)
                        </label>
                        <input 
                          type="number" 
                          className="search-input" 
                          min="1" 
                          max="180" 
                          style={{ width: '80px', display: 'block', margin: '0 auto', textAlign: 'center', fontSize: '13px', padding: '4px' }}
                          value={chainIdleMinutes}
                          onChange={(e) => setChainIdleMinutes(Math.max(1, parseInt(e.target.value) || 15))}
                        />
                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center', marginTop: '8px' }}>
                          Then, wait until system is completely idle for <strong>{chainIdleMinutes} minutes</strong>
                        </div>
                      </div>
                    )}

                    {selectedChainTrigger === 'download' && (
                      <div>
                        <label style={{ fontSize: '11px', color: 'var(--text-secondary)', display: 'block', marginBottom: '6px', textAlign: 'center' }}>
                          Chained Bandwidth threshold (KB/s)
                        </label>
                        <input 
                          type="number" 
                          className="search-input" 
                          min="1" 
                          style={{ width: '90px', display: 'block', margin: '0 auto', textAlign: 'center', fontSize: '13px', padding: '4px' }}
                          value={chainDownloadSpeed}
                          onChange={(e) => setChainDownloadSpeed(Math.max(1, parseInt(e.target.value) || 100))}
                        />
                        <div style={{ fontSize: '12px', color: 'var(--text-secondary)', textAlign: 'center', marginTop: '8px' }}>
                          Then, wait until download rate drops below <strong>{chainDownloadSpeed} KB/s</strong> for 3 minutes
                        </div>
                      </div>
                    )}

                    {selectedChainTrigger === 'jellyfin' && (
                      <div>
                        <label style={{ fontSize: '11px', color: 'var(--text-secondary)', display: 'block', marginBottom: '6px', textAlign: 'center' }}>
                          Chained Jellyfin local port
                        </label>
                        <input 
                          type="number" 
                          className="search-input" 
                          style={{ width: '90px', display: 'block', margin: '0 auto', textAlign: 'center', fontSize: '13px', padding: '4px' }}
                          value={chainJellyfinPort}
                          onChange={(e) => setChainJellyfinPort(Math.max(1, parseInt(e.target.value) || 8096))}
                        />
                        <div style={{ fontSize: '12px', color: '#00ffcc', textAlign: 'center', marginTop: '8px', fontWeight: 'bold' }}>
                          📺 Streaming hold guard will active on port {chainJellyfinPort}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>

            {/* Step 3: Policy Configuration */}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '24px', marginBottom: '30px' }}>
              
              {/* Execution Mode */}
              <div style={{ flex: 1, minWidth: '220px' }}>
                <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '8px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  3. Execution Mode Policy
                </label>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  {[
                    { id: 'smart', label: `Smart Hybrid (Graceful ${graceMinutes}m, then Force)`, desc: `Allows ${graceMinutes} mins to save before force close (Recommended)` },
                    { id: 'graceful', label: 'Graceful Prompt', desc: 'Prompts to save open files, lets apps block shutdown' },
                    { id: 'force', label: 'Force Close Immediately', desc: 'Instantly terminates all applications and actions' }
                  ].map((pol) => (
                    <div 
                      key={pol.id}
                      onClick={() => setSelectedMode(pol.id)}
                      style={{
                        background: selectedMode === pol.id ? 'var(--surface-color)' : 'transparent',
                        border: `1px solid ${selectedMode === pol.id ? 'var(--accent-color)' : 'var(--border-color)'}`,
                        borderRadius: '6px',
                        padding: '12px',
                        cursor: 'pointer',
                        transition: 'all 0.2s ease'
                      }}
                    >
                      <div style={{ fontWeight: 'bold', fontSize: '12px', color: selectedMode === pol.id ? 'white' : 'var(--text-secondary)' }}>{pol.label}</div>
                      <div style={{ fontSize: '10px', color: 'var(--text-secondary)', marginTop: '2px' }}>{pol.desc}</div>
                    </div>
                  ))}
                  {selectedMode === 'smart' && (
                    <div style={{ marginTop: '12px', background: 'rgba(255,255,255,0.02)', border: '1px solid rgba(255,255,255,0.04)', borderRadius: '6px', padding: '12px' }}>
                      <label style={{ fontSize: '11px', color: 'var(--text-secondary)', display: 'block', marginBottom: '6px' }}>
                        Smart Grace Delay: <strong>{graceMinutes} minutes</strong>
                      </label>
                      <input 
                        type="range" 
                        min="1" 
                        max="60" 
                        value={graceMinutes} 
                        onChange={(e) => setGraceMinutes(parseInt(e.target.value))}
                        style={{ width: '100%', accentColor: currentActionTheme.color }}
                      />
                    </div>
                  )}
                </div>
              </div>

              {/* Security Toggles */}
              <div style={{ flex: 1, minWidth: '220px' }}>
                <label style={{ fontSize: '12px', color: 'var(--text-secondary)', display: 'block', marginBottom: '8px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
                  4. Cancellation & Security Settings
                </label>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', background: 'var(--surface-color)', border: '1px solid var(--border-color)', borderRadius: '6px', padding: '16px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyBehavior: 'space-between', gap: '12px' }}>
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 'bold', fontSize: '12px' }}>Allow Cancellation</div>
                      <div style={{ fontSize: '10px', color: 'var(--text-secondary)' }}>Allow user to abort the countdown schedule</div>
                    </div>
                    <input 
                      type="checkbox" 
                      style={{ width: '18px', height: '18px', accentColor: 'var(--accent-color)' }}
                      checked={allowCancel}
                      onChange={(e) => setAllowCancel(e.target.checked)}
                    />
                  </div>
                  
                  {/* Warning message for forced schedules */}
                  {!allowCancel && (
                    <div style={{
                      color: 'var(--danger-color)',
                      background: 'rgba(255, 51, 102, 0.05)',
                      padding: '8px',
                      borderRadius: '4px',
                      fontSize: '10px',
                      border: '1px solid rgba(255, 51, 102, 0.2)'
                    }}>
                      ⚠️ Once scheduled, this action is locked and CANNOT be stopped. Use with caution.
                    </div>
                  )}

                  {allowCancel && (
                    <div style={{ fontSize: '10px', color: 'var(--text-secondary)', borderTop: '1px solid var(--border-color)', paddingTop: '10px' }}>
                      🔒 If FoxWall settings are currently password-locked, cancellation will automatically require entering your security password.
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* Launch Button */}
            <button 
              className="filter-btn active"
              onClick={handleSchedule}
              style={{
                background: currentActionTheme.color,
                borderColor: currentActionTheme.color,
                color: 'white',
                width: '100%',
                padding: '16px',
                borderRadius: '8px',
                fontSize: '16px',
                fontWeight: 'bold',
                boxShadow: `0 0 15px ${currentActionTheme.glow}`,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '8px'
              }}
            >
              <Power size={18} />
              Initiate Scheduled {selectedAction.charAt(0).toUpperCase() + selectedAction.slice(1)}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
