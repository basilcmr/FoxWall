import React from 'react';
import { Copy, Search, ShieldAlert } from 'lucide-react';

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

  // Look up real path and hash from live/log data if missing
  const cleanName = processName ? processName.trim() : '';
  if (cleanName && (!resolvedPath || !resolvedHash)) {
    const socketMatch = socketData.find(
      (s) => s.ProcessName && s.ProcessName.toLowerCase() === cleanName.toLowerCase()
    );
    if (socketMatch) {
      if (!resolvedPath) resolvedPath = socketMatch.Path;
      if (!resolvedHash) resolvedHash = socketMatch.FileHash;
    } else {
      const logMatch = logData.find(
        (l) => l.ProcessName && l.ProcessName.toLowerCase() === cleanName.toLowerCase()
      );
      if (logMatch) {
        if (!resolvedPath) resolvedPath = logMatch.Path;
        if (!resolvedHash) resolvedHash = logMatch.FileHash;
      }
    }
  }

  if (!resolvedPath && cleanName) {
    resolvedPath = `C:\\Windows\\System32\\${cleanName}.exe`;
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
          onOpenSearch(cleanName, resolvedPath);
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
    </span>
  );
}
