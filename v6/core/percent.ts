// Port of MetricMath.cs. A percentage the owner acts on never claims more than the counts support: 100 only
// when nothing is outstanding, 0 only when nothing qualified. Plain rounding put "100% caught up" beside
// "4 awaiting" on the same v5 card (996/1000), and "SLA met 100%" beside a breach.
// Math.round rounds .5 up where C# rounded to even, so 1/8 reads 13 rather than 12. Both are honest.
export function honestPercent(part: number, total: number): number {
  if (total <= 0 || part >= total) return 100;
  if (part <= 0) return 0;
  return Math.min(99, Math.max(1, Math.round((part / total) * 100)));
}
