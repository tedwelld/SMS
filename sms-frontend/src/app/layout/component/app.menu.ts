import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { MenuItem } from 'primeng/api';

import { PortalRole } from '@/app/core/auth.models';
import { AuthService } from '@/app/core/auth.service';
import { SmsStoreService } from '@/app/core/sms-store.service';
import { AppMenuitem } from './app.menuitem';

interface AppMenuItem extends MenuItem {
  roles?: PortalRole[];
  items?: AppMenuItem[];
}

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [CommonModule, AppMenuitem, RouterModule],
  template: `<ul class="layout-menu">
    @for (item of model; track item.label) {
      @if (!item.separator) {
        <li app-menuitem [item]="item" [root]="true"></li>
      } @else {
        <li class="menu-separator"></li>
      }
    }
  </ul>`
})
export class AppMenu {
  private readonly auth = inject(AuthService);
  private readonly store = inject(SmsStoreService);
  private readonly router = inject(Router);

  model: AppMenuItem[] = [];

  ngOnInit() {
    const items: AppMenuItem[] = [
      {
        label: 'Operations',
        items: [
          { label: 'Dashboard', icon: 'pi pi-fw pi-home', routerLink: ['/dashboard'] },
          { label: 'POS', icon: 'pi pi-fw pi-shopping-cart', routerLink: ['/pos'] },
          { label: 'Inventory', icon: 'pi pi-fw pi-box', routerLink: ['/inventory'] },
          { label: 'Procurement', icon: 'pi pi-fw pi-truck', routerLink: ['/procurement'] },
          { label: 'Reports', icon: 'pi pi-fw pi-chart-bar', routerLink: ['/reports'] }
        ]
      },
      {
        label: 'Administration',
        roles: ['admin'],
        items: [
          { label: 'Staff Admin', icon: 'pi pi-fw pi-user-edit', routerLink: ['/admin/staff'] },
          { label: 'Settings', icon: 'pi pi-fw pi-cog', routerLink: ['/settings'] }
        ]
      },
      {
        label: 'Session',
        items: [
          {
            label: 'Logout',
            icon: 'pi pi-fw pi-sign-out',
            command: () => {
              this.auth.logout();
              this.store.clearCart();
              void this.router.navigateByUrl('/auth/login');
            }
          }
        ]
      }
    ];

    this.model = this.filterByRole(items, this.auth.role);
  }

  private filterByRole(items: AppMenuItem[], role: PortalRole): AppMenuItem[] {
    return items
      .filter((item) => !item.roles || item.roles.includes(role))
      .map((item) => ({
        ...item,
        items: item.items ? this.filterByRole(item.items, role) : undefined
      }))
      .filter((item) => !item.items || item.items.length > 0);
  }
}
