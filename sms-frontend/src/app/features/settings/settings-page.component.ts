import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { SmsStoreService } from '../../core/sms-store.service';
import { SystemPreferencesService } from '../../core/system-preferences.service';

@Component({
  selector: 'app-settings-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './settings-page.component.html',
  styleUrl: './settings-page.component.scss'
})
export class SettingsPageComponent {
  readonly store = inject(SmsStoreService);
  readonly preferences = inject(SystemPreferencesService);

  readonly activeRole = signal(this.store.activeRole());
  readonly offlineMode = signal(this.store.offlineMode());

  readonly cardRadius = signal(this.preferences.preferences().cardRadius);
  readonly softContrast = signal(this.preferences.preferences().softContrast);

  readonly saveMessage = signal<string | null>(null);

  readonly roleOptions = computed(() => this.store.roles);

  applySystemSettings(): void {
    this.saveMessage.set(null);

    if (this.activeRole() !== this.store.activeRole()) {
      this.store.setActiveRole(this.activeRole());
    }

    if (this.offlineMode() !== this.store.offlineMode()) {
      this.store.setOfflineMode(this.offlineMode());
    }

    this.preferences.update({
      cardRadius: this.cardRadius(),
      softContrast: this.softContrast()
    });

    this.saveMessage.set('Settings saved and applied.');
  }

  resetAppearance(): void {
    this.preferences.reset();
    const defaults = this.preferences.preferences();
    this.cardRadius.set(defaults.cardRadius);
    this.softContrast.set(defaults.softContrast);
    this.saveMessage.set('Appearance preferences reset.');
  }
}
