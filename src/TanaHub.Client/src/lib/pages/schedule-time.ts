export type ScheduleRange = 'Today' | 'Tomorrow' | 'This week' | 'Next week';

export function dayKey(value: Date, zone: string): string {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(value);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? '';
  return `${part('year')}-${part('month')}-${part('day')}`;
}

export function offsetDay(key: string, days: number): string {
  const date = new Date(`${key}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** Resolve midnight in an IANA timezone, including daylight-saving offsets. */
export function dayStart(key: string, zone: string): Date {
  const [year, month, day] = key.split('-').map(Number);
  const midnight = Date.UTC(year, month - 1, day);
  let instant = midnight;
  const formatter = new Intl.DateTimeFormat('en-US', { timeZone: zone, year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric', hourCycle: 'h23' });
  for (let iteration = 0; iteration < 3; iteration += 1) {
    const parts = formatter.formatToParts(new Date(instant));
    const part = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find(item => item.type === type)?.value ?? 0);
    const displayed = Date.UTC(part('year'), part('month') - 1, part('day'), part('hour'), part('minute'), part('second'));
    const resolved = midnight - (displayed - instant);
    if (resolved === instant) break;
    instant = resolved;
  }
  return new Date(instant);
}

export function scheduleWindow(range: ScheduleRange, zone: string, now = new Date()): { from: string; to: string } {
  const today = dayKey(now, zone);
  const start = range === 'Tomorrow' ? dayStart(offsetDay(today, 1), zone)
    : range === 'Next week' ? dayStart(offsetDay(today, 7), zone) : now;
  const end = dayStart(offsetDay(today, range === 'Next week' ? 14 : range === 'This week' ? 7 : range === 'Tomorrow' ? 2 : 1), zone);
  return { from: start.toISOString(), to: end.toISOString() };
}
