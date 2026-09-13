---
id: v6-extra-windows-inherit-the-main-close-hook
date: 2026-09-13
agent: claude
title: A close hook added in browser-window-created applies to every window, so a hidden helper window's close would hide or quit the app
triggers: [BrowserWindow, hidden window, printToPDF, capturePage, browser-window-created, close, hide to tray, quit, window-all-closed, clipboard, writeImage, ClipboardItem]
files: [v6/app/main.ts, v6/ui/print.tsx]
cost: Caught while adding the report window, before it shipped; the clipboard call failed typecheck.
status: live
---

`main.ts` used to register the hide-or-quit close handler with `app.on('browser-window-created', …)`, which attaches it to every window ever created, not just the main one. The hidden window that draws the weekly report for PDF and image export would have run `closeWindow()` on close, hiding the main window or quitting the app. The handler is now on `win` itself, and helper windows are ended with `destroy()`, which skips `close` entirely. Two more things that apply to any helper window: messages it sends to `ipcMain` (for example `ready`) also reach the global handlers, so a helper page must not send `navigate` or other messages that change the main window's state (the print page renders only the report and sends `ready` and `print-rendered`); and a hidden `BrowserWindow` (`show: false`) does paint, so `capturePage()` returns a real image once `setContentSize` has been given a moment. Separately, Electron 44's `clipboard` is the W3C-style async API: `clipboard.writeImage` no longer exists; write `new ClipboardItem({ 'image/png': new Blob([png], { type: 'image/png' }) })` with `await clipboard.write([...])`.
