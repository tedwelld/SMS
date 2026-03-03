import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';

import { AuthService } from '@/app/core/auth.service';
import { SmsStoreService } from '@/app/core/sms-store.service';
import { SYSTEM_BRANDING } from '@/app/core/system-branding';
import { LayoutService } from '@/app/layout/service/layout.service';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [RouterModule, CommonModule],
  template: ` <div class="layout-topbar">
    <div class="layout-topbar-logo-container">
      <button class="layout-menu-button layout-topbar-action" (click)="layoutService.onMenuToggle()">
        <i class="pi pi-bars"></i>
      </button>
      <a class="layout-topbar-logo" routerLink="/dashboard">
        <img class="logo-mark" [src]="branding.logoPath" [alt]="branding.name + ' logo'" />
        <span>{{ branding.name }}</span>
      </a>
    </div>

    <div class="layout-topbar-actions">
      <div class="layout-topbar-menu hidden lg:block">
        <div class="layout-topbar-menu-content">
          <button type="button" class="layout-topbar-action" disabled>
            <i class="pi pi-user"></i>
            <span>{{ auth.session()?.displayName || 'Staff User' }}</span>
          </button>
          <button type="button" class="layout-topbar-action btn-danger-action" (click)="logout()">
            <i class="pi pi-sign-out"></i>
            <span>Logout</span>
          </button>
        </div>
      </div>
    </div>
  </div>`
})
export class AppTopbar {
  readonly auth = inject(AuthService);
  readonly branding = SYSTEM_BRANDING;

  private readonly router = inject(Router);
  private readonly store = inject(SmsStoreService);

  layoutService = inject(LayoutService);

  logout() {
    this.auth.logout();
    this.store.clearCart();
    void this.router.navigateByUrl('/auth/login');
  }
}
