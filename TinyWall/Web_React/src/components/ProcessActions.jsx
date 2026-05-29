import React from 'react';
import { Copy, Search, ShieldAlert, FolderOpen } from 'lucide-react';

export default function ProcessActions({
  processName,
  path,
  fileHash,
  socketData = [],
  logData = [],
  onOpenCopy,
  onOpenSearch,
  searchVirusTotal,
  alwaysVisible = false
}) {
  // Centralized resolution of path and fileHash
  let resolvedPath = path || '';
  let resolvedHash = fileHash || '';
  let resolvedAppName = '';

  // Look up real path and hash from live/log data if missing
  const cleanName = processName ? processName.trim() : '';
  if (cleanName) {
    const socketMatch = socketData.find(
      (s) => {
        if (!s.ProcessName) return false;
        const sName = s.ProcessName.toLowerCase();
        const cName = cleanName.toLowerCase();
        return sName === cName || sName === `${cName}.exe` || `${sName}.exe` === cName || sName.replace(/\.exe$/, '') === cName.replace(/\.exe$/, '');
      }
    );
    if (socketMatch) {
      if (!resolvedPath) resolvedPath = socketMatch.Path;
      if (!resolvedHash) resolvedHash = socketMatch.FileHash;
      resolvedAppName = socketMatch.AppName || '';
    } else {
      const logMatch = logData.find(
        (l) => {
          if (!l.ProcessName) return false;
          const lName = l.ProcessName.toLowerCase();
          const cName = cleanName.toLowerCase();
          return lName === cName || lName === `${cName}.exe` || `${lName}.exe` === cName || lName.replace(/\.exe$/, '') === cName.replace(/\.exe$/, '');
        }
      );
      if (logMatch) {
        if (!resolvedPath) resolvedPath = logMatch.Path;
        if (!resolvedHash) resolvedHash = logMatch.FileHash;
        resolvedAppName = logMatch.AppName || '';
      }
    }
  }

  if (!resolvedPath && cleanName) {
    resolvedPath = `C:\\Windows\\System32\\${cleanName.endsWith('.exe') ? cleanName : `${cleanName}.exe`}`;
  }

  const safePath = resolvedPath.replace(/\\/g, '\\\\');

  const actionStyle = alwaysVisible
    ? { opacity: 1, transform: 'scale(1)', marginLeft: '10px' }
    : {};

  return (
    <span className="task-actions-inline" style={actionStyle}>
      <span
        className="inline-action-btn"
        title="Copy to Clipboard"
        onClick={(e) => {
          e.stopPropagation();
          onOpenCopy(cleanName, resolvedPath);
        }}
      >
        <Copy size={12} />
      </span>
      <span
        className="inline-action-btn"
        title="Search Google"
        onClick={(e) => {
          e.stopPropagation();
          onOpenSearch(cleanName, resolvedPath, resolvedAppName);
        }}
      >
        <Search size={12} />
      </span>
      <span
        className="inline-action-btn"
        title="Search on VirusTotal"
        onClick={(e) => {
          e.stopPropagation();
          searchVirusTotal(resolvedPath, resolvedHash);
        }}
      >
        <ShieldAlert size={12} />
      </span>
      {/* [FoxWall Enhancement] - Open File Location Button */}
      <span
        className="inline-action-btn"
        title="Open File Location"
        onClick={async (e) => {
          e.stopPropagation();
          try {
            await fetch(`/api/action/open-folder?path=${encodeURIComponent(resolvedPath)}`, { method: 'POST' });
          } catch (err) {
            console.error("Failed to open file location:", err);
          }
        }}
      >
        <FolderOpen size={12} />
      </span>
      {/* [FoxWall Enhancement] - End of Open File Location Button */}
    </span>
  );
}
