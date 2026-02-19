import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';

import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-reports-page',
  imports: [CommonModule, CurrencyPipe, MatButtonModule, MatCardModule, MatTabsModule],
  templateUrl: './reports-page.component.html',
  styleUrl: './reports-page.component.scss'
})
export class ReportsPageComponent {
  readonly store = inject(SmsStoreService);

  readonly eod = computed(() => this.store.eodReport());

  readonly shrinkageRows = computed(() =>
    this.store
      .shrinkageReport()
      .sort((a, b) => Math.abs(b.variance) - Math.abs(a.variance))
      .slice(0, 8)
  );

  readonly maxTrendValue = computed(() => {
    const trend = this.store.salesTrend();
    const max = Math.max(...trend.map((slot) => slot.sales));
    return max > 0 ? max : 1;
  });

  widthFor(sales: number): string {
    return `${(sales / this.maxTrendValue()) * 100}%`;
  }
}
