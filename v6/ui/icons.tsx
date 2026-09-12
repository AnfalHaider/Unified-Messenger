// The icon set from the Front Desk renders: a 16-unit grid, 1.7 stroke, drawn not filled. One small map rather
// than an icon dependency; each value is SVG markup because a few icons need more than one element.
const MARKUP = {
  chat: "<path d=\"M3.5 13.5l1-3A5.5 5.5 0 1 1 7 13\" /><path d=\"M3.5 13.5l3-.6\"/>",
  ig: "<rect x=\"2.5\" y=\"2.5\" width=\"11\" height=\"11\" rx=\"3.2\"/><circle cx=\"8\" cy=\"8\" r=\"2.6\"/><circle cx=\"11.3\" cy=\"4.7\" r=\".6\" fill=\"currentColor\"/>",
  star: "<path d=\"M8 2.3l1.7 3.6 3.9.5-2.9 2.7.8 3.9L8 11.1 4.5 13l.8-3.9-2.9-2.7 3.9-.5z\"/>",
  line: "<path d=\"M2 11h12\"/><circle cx=\"4.5\" cy=\"7\" r=\"1.8\"/><circle cx=\"9\" cy=\"5\" r=\"1.8\"/><circle cx=\"12.5\" cy=\"8\" r=\"1.8\"/>",
  grid: "<rect x=\"2.5\" y=\"2.5\" width=\"4.5\" height=\"4.5\" rx=\"1\"/><rect x=\"9\" y=\"2.5\" width=\"4.5\" height=\"4.5\" rx=\"1\"/><rect x=\"2.5\" y=\"9\" width=\"4.5\" height=\"4.5\" rx=\"1\"/><rect x=\"9\" y=\"9\" width=\"4.5\" height=\"4.5\" rx=\"1\"/>",
  gear: "<circle cx=\"8\" cy=\"8\" r=\"2.2\"/><path d=\"M8 1.8v1.8M8 12.4v1.8M1.8 8h1.8M12.4 8h1.8M3.6 3.6l1.3 1.3M11.1 11.1l1.3 1.3M3.6 12.4l1.3-1.3M11.1 4.9l1.3-1.3\"/>",
  sun: "<circle cx=\"8\" cy=\"8\" r=\"2.8\"/><path d=\"M8 1.5v1.6M8 12.9v1.6M1.5 8h1.6M12.9 8h1.6M3.4 3.4l1.1 1.1M11.5 11.5l1.1 1.1M3.4 12.6l1.1-1.1M11.5 4.5l1.1-1.1\"/>",
  moon: "<path d=\"M12.8 9.6A5.2 5.2 0 0 1 6.4 3.2 5.2 5.2 0 1 0 12.8 9.6z\"/>",
  monitor: "<rect x=\"2\" y=\"3\" width=\"12\" height=\"8\" rx=\"1.3\"/><path d=\"M6 14h4M8 11v3\"/>",
  refresh: "<path d=\"M13 8a5 5 0 1 1-1.5-3.6\"/><path d=\"M13 2.5v3h-3\"/>",
  check: "<path d=\"M3 8.5l3 3 7-7\"/>",
  open: "<path d=\"M9 3h4v4M13 3L7.5 8.5\"/><path d=\"M11 9.5V13H3V5h3.5\"/>",
  snooze: "<circle cx=\"8\" cy=\"8.5\" r=\"5\"/><path d=\"M8 5.8v2.9l2 1.2M3 2.5l2 1.3M13 2.5l-2 1.3\"/>",
  lock: "<rect x=\"3.5\" y=\"7\" width=\"9\" height=\"6.5\" rx=\"1.3\"/><path d=\"M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7\"/>",
  alert: "<path d=\"M8 2.5l6 10.5H2z\"/><path d=\"M8 6.5v3M8 11.3v.2\"/>",
  bell: "<path d=\"M4 11V7a4 4 0 0 1 8 0v4l1.2 1.5H2.8z\"/><path d=\"M6.6 14a1.5 1.5 0 0 0 2.8 0\"/>",
  x: "<path d=\"M4 4l8 8M12 4l-8 8\"/>",
  min: "<path d=\"M3.5 8h9\"/>",
  max: "<rect x=\"3.5\" y=\"3.5\" width=\"9\" height=\"9\" rx=\"1\"/>",
  right: "<path d=\"M6 3.5L10.5 8 6 12.5\"/>",
  qr: "<rect x=\"2.5\" y=\"2.5\" width=\"4\" height=\"4\"/><rect x=\"9.5\" y=\"2.5\" width=\"4\" height=\"4\"/><rect x=\"2.5\" y=\"9.5\" width=\"4\" height=\"4\"/><path d=\"M9.5 9.5h1.5v1.5M13.5 9.5v4h-4\"/>",
  sleep: "<path d=\"M12.8 9.6A5.2 5.2 0 0 1 6.4 3.2 5.2 5.2 0 1 0 12.8 9.6z\"/><path d=\"M10 2.5h3l-3 3h3\"/>",
  reviews: "<path d=\"M8 2.3l1.7 3.6 3.9.5-2.9 2.7.8 3.9L8 11.1 4.5 13l.8-3.9-2.9-2.7 3.9-.5z\"/>",
  chart: "<path d=\"M2.5 13.5h11\"/><path d=\"M4 11V8M7 11V4.5M10 11V6.5M13 11V9\"/>",
  spark: "<path d=\"M8 2v3M8 11v3M2 8h3M11 8h3M4 4l2 2M10 10l2 2M4 12l2-2M10 6l2-2\"/>",
  users: "<circle cx=\"6\" cy=\"5.5\" r=\"2.3\"/><path d=\"M1.8 13.5a4.2 4.2 0 0 1 8.4 0\"/><path d=\"M10.5 3.4a2.3 2.3 0 0 1 0 4.3M12 9.6a4.2 4.2 0 0 1 2.2 3.9\"/>",
  cloud: "<path d=\"M4.5 12.5h7a3 3 0 0 0 .4-6 4 4 0 0 0-7.7 1A2.5 2.5 0 0 0 4.5 12.5z\"/>",
  download: "<path d=\"M8 2.5v8M4.8 7.5L8 10.7l3.2-3.2M3 13.5h10\"/>",
  copy: "<rect x=\"5.5\" y=\"5.5\" width=\"8\" height=\"8\" rx=\"1.4\"/><path d=\"M10.5 5.5V3.2a.7.7 0 0 0-.7-.7H3.2a.7.7 0 0 0-.7.7v6.6c0 .4.3.7.7.7h2.3\"/>",
  phone: "<path d=\"M4.2 2.5h2l1 3-1.5 1a7 7 0 0 0 3.8 3.8l1-1.5 3 1v2a1.5 1.5 0 0 1-1.6 1.5A11 11 0 0 1 2.7 4.1 1.5 1.5 0 0 1 4.2 2.5z\"/>",
  note: "<path d=\"M3.5 2.5h6l3 3v8h-9z\"/><path d=\"M9.5 2.5v3h3M5.5 8.5h5M5.5 11h3.5\"/>",
  tag: "<path d=\"M2.5 2.5h5.3l5.7 5.7-5.3 5.3-5.7-5.7z\"/><circle cx=\"5.3\" cy=\"5.3\" r=\"1\"/>",
  cal: "<rect x=\"2.5\" y=\"3.5\" width=\"11\" height=\"10\" rx=\"1.3\"/><path d=\"M2.5 6.5h11M5.5 2v3M10.5 2v3\"/>",
  shield: "<path d=\"M8 2l5 2v4c0 3-2.2 5.2-5 6-2.8-.8-5-3-5-6V4z\"/>",
  offline: "<path d=\"M2 5.5a9 9 0 0 1 12 0M4.3 8a5.7 5.7 0 0 1 7.4 0M6.6 10.5a2.3 2.3 0 0 1 2.8 0\"/><path d=\"M2.5 2.5l11 11\"/>",
  search: "<circle cx=\"7\" cy=\"7\" r=\"4.3\"/><path d=\"M10.2 10.2l3.3 3.3\"/>",
  export: "<path d=\"M8 10V2.5M4.8 5.7L8 2.5l3.2 3.2\"/><path d=\"M3 9v4.5h10V9\"/>",
  sunrise: "<path d=\"M2 12.5h12M4 12.5a4 4 0 0 1 8 0M8 3v3M3.5 6.5l1.2 1.2M12.5 6.5l-1.2 1.2\"/>",
  reopen: "<path d=\"M3 8a5 5 0 0 1 8.6-3.5L13 6\"/><path d=\"M13 2.5V6H9.5\"/><path d=\"M13 8a5 5 0 0 1-8.6 3.5L3 10\"/>",
  key: "<circle cx=\"5.5\" cy=\"10.5\" r=\"2.8\"/><path d=\"M7.5 8.5l6-6M11 5l2 2M9.5 6.5l1.5 1.5\"/>",
  send: "<path d=\"M2.5 8L13.5 2.5 11 13.5 8 9.5z\"/><path d=\"M8 9.5l5.5-7\"/>",
  clock: "<circle cx=\"8\" cy=\"8\" r=\"5.5\"/><path d=\"M8 5v3.2l2.2 1.3\"/>",
  more: "<circle cx=\"4\" cy=\"8\" r=\".9\" fill=\"currentColor\"/><circle cx=\"8\" cy=\"8\" r=\".9\" fill=\"currentColor\"/><circle cx=\"12\" cy=\"8\" r=\".9\" fill=\"currentColor\"/>",
} as const;

export type IconName = keyof typeof MARKUP;

export function Icon({ name, size = 16, stroke = 1.7 }: { name: IconName; size?: number; stroke?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth={stroke}
      strokeLinecap="round" strokeLinejoin="round" style={{ flex: 'none' }} aria-hidden="true"
      dangerouslySetInnerHTML={{ __html: MARKUP[name] }} />
  );
}

/** A channel's icon. A channel with no reader still gets a face, so the grid never has a hole in it. */
export const channelIcon = (channel: string): IconName =>
  channel === 'instagram' ? 'ig' : channel === 'googlebusiness' ? 'star' : channel === 'whatsapp' || channel === 'whatsappbusiness' ? 'chat' : 'more';
