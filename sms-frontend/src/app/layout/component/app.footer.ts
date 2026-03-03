import { Component } from '@angular/core';
import { SYSTEM_BRANDING, SYSTEM_BRANDING_FULL_ADDRESS } from '@/app/core/system-branding';

@Component({
  standalone: true,
  selector: 'app-footer',
  template: `<footer class="layout-footer">
    <img class="system-logo" [src]="branding.logoPath" [alt]="branding.name + ' logo'" />
    <div class="system-meta">
      <strong>{{ branding.name }}</strong>
      <span>{{ branding.email }} | {{ branding.phone }}</span>
      <span>{{ fullAddress }}</span>
    </div>
  </footer>`
})
export class AppFooter {
  readonly branding = SYSTEM_BRANDING;
  readonly fullAddress = SYSTEM_BRANDING_FULL_ADDRESS;
}
