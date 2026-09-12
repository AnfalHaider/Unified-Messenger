// The registry. A channel with no entry here produces no figures, and the screens say so rather than showing
// zeroes — which is the whole point of keeping the list short and honest.
import { instagram } from './instagram/index.ts';
import { whatsapp } from './whatsapp/index.ts';
import type { ChannelModule } from './types.ts';

/** Channel id → module. WhatsApp Business is the same page and the same reader under another account. */
export const MODULES: Record<string, ChannelModule> = {
  whatsapp,
  whatsappbusiness: { ...whatsapp, id: 'whatsappbusiness', name: 'WhatsApp Business' },
  instagram,
};

export const moduleFor = (channel: string): ChannelModule | undefined => MODULES[channel];

/** How a module is doing, so the app can show a health light per channel instead of one vague status. */
export interface ModuleHealth {
  id: string;
  name: string;
  /** Reads that produced chats, and reads that threw or returned nothing usable, since the app started. */
  ok: number;
  failed: number;
  lastOkAt: number | null;
  lastError: string | null;
}

export const newHealth = (m: ChannelModule): ModuleHealth => ({ id: m.id, name: m.name, ok: 0, failed: 0, lastOkAt: null, lastError: null });

export type { ChannelModule, ReadResult, SignInState } from './types.ts';
