import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export interface UiPreferences {
  cardRadius: number;
  softContrast: boolean;
}

const DEFAULT_PREFERENCES: UiPreferences = {
  cardRadius: 22,
  softContrast: true
};

const STORAGE_KEY = 'sms_ui_preferences_v1';

@Injectable({ providedIn: 'root' })
export class SystemPreferencesService {
  private readonly document = inject(DOCUMENT);

  readonly preferences = signal<UiPreferences>(DEFAULT_PREFERENCES);

  constructor() {
    this.restore();

    effect(() => {
      const prefs = this.preferences();
      const root = this.document.documentElement;

      root.style.setProperty('--sms-card-radius', `${Math.max(16, Math.min(32, prefs.cardRadius))}px`);
      root.classList.toggle('sms-soft-contrast', prefs.softContrast);

      localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
    });
  }

  update(patch: Partial<UiPreferences>): void {
    this.preferences.update((state) => ({
      ...state,
      ...patch
    }));
  }

  reset(): void {
    this.preferences.set(DEFAULT_PREFERENCES);
  }

  private restore(): void {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<UiPreferences>;
        this.preferences.set({
          cardRadius: parsed.cardRadius ?? DEFAULT_PREFERENCES.cardRadius,
          softContrast: parsed.softContrast ?? DEFAULT_PREFERENCES.softContrast
        });
      }
    } catch {
      this.preferences.set(DEFAULT_PREFERENCES);
    }
  }
}
