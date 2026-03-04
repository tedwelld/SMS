import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { SmsStoreService } from '../../core/sms-store.service';
import { SYSTEM_BRANDING, SYSTEM_BRANDING_FULL_ADDRESS } from '../../core/system-branding';

type ReportSection = 'eod' | 'shrinkage' | 'sales';
type SelectedReportSection = ReportSection | 'all';

type HorizontalAlign = 'left' | 'right' | 'center';

interface ReportTableColumn {
  header: string;
  widthRatio: number;
  align?: HorizontalAlign;
}

interface ReportRenderContext {
  title: string;
  generatedAt: string;
  logoDataUrl: string | null;
}

interface CapturedVisualSnapshot {
  dataUrl: string;
  widthPx: number;
  heightPx: number;
}

@Component({
  selector: 'app-reports-page',
  imports: [CommonModule, CurrencyPipe, FormsModule, MatButtonModule, MatCardModule],
  templateUrl: './reports-page.component.html',
  styleUrl: './reports-page.component.scss'
})
export class ReportsPageComponent {
  readonly store = inject(SmsStoreService);
  readonly branding = SYSTEM_BRANDING;
  readonly selectedReport = signal<SelectedReportSection>('all');
  readonly filterQuery = signal('');
  readonly varianceFilter = signal<'all' | 'loss' | 'gain' | 'balanced'>('all');
  readonly minSales = signal(0);
  readonly exporting = signal(false);

  @ViewChild('eodVisualBlock') eodVisualBlock?: ElementRef<HTMLElement>;
  @ViewChild('salesVisualBlock') salesVisualBlock?: ElementRef<HTMLElement>;

  private readonly reportLayout = {
    left: 14,
    right: 14,
    headerTop: 10,
    headerBottom: 36,
    contentTop: 43,
    contentBottom: 275,
    footerTop: 284
  };

  private cachedLogoPngDataUrl: string | null | undefined = undefined;

  readonly eod = computed(() => this.store.eodReport());

  readonly shrinkageRows = computed(() =>
    this.store
      .shrinkageReport()
      .sort((a, b) => Math.abs(b.variance) - Math.abs(a.variance))
  );

  readonly filteredShrinkageRows = computed(() => {
    const query = this.filterQuery().trim().toLowerCase();
    const filter = this.varianceFilter();

    return this.shrinkageRows().filter((row) => {
      const matchesSearch = !query
        || row.product.toLowerCase().includes(query)
        || row.sku.toLowerCase().includes(query);

      const matchesVariance = filter === 'all'
        || (filter === 'loss' && row.variance < 0)
        || (filter === 'gain' && row.variance > 0)
        || (filter === 'balanced' && row.variance === 0);

      return matchesSearch && matchesVariance;
    });
  });

  readonly filteredSalesTrend = computed(() => {
    const query = this.filterQuery().trim().toLowerCase();
    const min = Math.max(0, this.minSales());

    return this.store.salesTrend().filter((slot) =>
      (!query || slot.hour.toLowerCase().includes(query)) && slot.sales >= min
    );
  });

  readonly maxTrendValue = computed(() => {
    const trend = this.filteredSalesTrend();
    if (trend.length === 0) {
      return 1;
    }

    const max = Math.max(...trend.map((slot) => slot.sales));
    return max > 0 ? max : 1;
  });

  widthFor(sales: number): string {
    return `${(sales / this.maxTrendValue()) * 100}%`;
  }

  async exportPdf(section?: ReportSection): Promise<void> {
    this.exporting.set(true);

    try {
      const effectiveSelection: SelectedReportSection = section ?? this.selectedReport();
      const visuals = await this.captureVisualSnapshots(effectiveSelection);
      const { jsPDF } = await import('jspdf');
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const context: ReportRenderContext = {
        title: this.getReportTitle(effectiveSelection),
        generatedAt: new Date().toLocaleString(),
        logoDataUrl: await this.getLogoDataUrlForPdf()
      };

      this.drawPageFrame(doc, context);

      let y = this.reportLayout.contentTop;
      if (effectiveSelection === 'eod') {
        y = this.renderEodReport(doc, y, context);
        y = this.writeVisualSnapshot(doc, y, 'EOD Visuals', visuals.eod, context);
      } else if (effectiveSelection === 'shrinkage') {
        y = this.renderShrinkageReport(doc, y, context);
      } else if (effectiveSelection === 'sales') {
        y = this.renderSalesReport(doc, y, context);
        y = this.writeVisualSnapshot(doc, y, 'Sales Trend Visual', visuals.sales, context);
      } else {
        y = this.renderEodReport(doc, y, context);
        y = this.writeVisualSnapshot(doc, y, 'EOD Visuals', visuals.eod, context);
        y = this.ensureSpace(doc, y + 4, 40, context);
        y = this.renderShrinkageReport(doc, y, context);
        y = this.ensureSpace(doc, y + 4, 40, context);
        y = this.renderSalesReport(doc, y, context);
        y = this.writeVisualSnapshot(doc, y, 'Sales Trend Visual', visuals.sales, context);
      }

      this.writeNote(doc, y, this.getReportExplanation(effectiveSelection), context);

      doc.save(`sms-${effectiveSelection}-report.pdf`);
    } finally {
      this.exporting.set(false);
    }
  }

  private async captureVisualSnapshots(
    section: SelectedReportSection
  ): Promise<{ eod?: CapturedVisualSnapshot; sales?: CapturedVisualSnapshot }> {
    const snapshots: { eod?: CapturedVisualSnapshot; sales?: CapturedVisualSnapshot } = {};

    if (section === 'eod' || section === 'all') {
      const eodSnapshot = await this.captureElementSnapshot(this.eodVisualBlock?.nativeElement);
      if (eodSnapshot) {
        snapshots.eod = eodSnapshot;
      }
    }

    if (section === 'sales' || section === 'all') {
      const salesSnapshot = await this.captureElementSnapshot(this.salesVisualBlock?.nativeElement);
      if (salesSnapshot) {
        snapshots.sales = salesSnapshot;
      }
    }

    return snapshots;
  }

  private async captureElementSnapshot(element?: HTMLElement): Promise<CapturedVisualSnapshot | undefined> {
    if (!element) {
      return undefined;
    }

    try {
      const html2canvasModule = await import('html2canvas');
      const html2canvas = html2canvasModule.default;
      const canvas = await html2canvas(element, {
        backgroundColor: '#ffffff',
        scale: 2,
        useCORS: true,
        logging: false
      });

      return {
        dataUrl: canvas.toDataURL('image/png'),
        widthPx: canvas.width,
        heightPx: canvas.height
      };
    } catch {
      return undefined;
    }
  }

  private writeVisualSnapshot(
    doc: import('jspdf').jsPDF,
    startY: number,
    heading: string,
    snapshot: CapturedVisualSnapshot | undefined,
    context: ReportRenderContext
  ): number {
    if (!snapshot) {
      return startY;
    }

    let y = this.ensureSpace(doc, startY + 2, 20, context);
    y = this.writeSectionHeading(doc, y, heading);

    const pageWidth = doc.internal.pageSize.getWidth();
    const availableWidth = pageWidth - this.reportLayout.left - this.reportLayout.right;
    const ratio = snapshot.widthPx > 0 ? snapshot.heightPx / snapshot.widthPx : 0.58;
    const imageHeight = Math.max(34, Math.min(105, availableWidth * ratio));
    y = this.ensureSpace(doc, y, imageHeight + 3, context);

    doc.setDrawColor(210, 217, 230);
    doc.rect(this.reportLayout.left, y, availableWidth, imageHeight);
    doc.addImage(snapshot.dataUrl, 'PNG', this.reportLayout.left, y, availableWidth, imageHeight);
    y += imageHeight + 4;
    return y;
  }

  private renderEodReport(doc: import('jspdf').jsPDF, startY: number, context: ReportRenderContext): number {
    let y = this.writeSectionHeading(doc, startY, 'End-of-Day Summary');
    const eod = this.eod();

    const rows: string[][] = [
      ['Cash Collections', this.formatCurrency(eod.cash)],
      ['Card Collections', this.formatCurrency(eod.card)],
      ['EcoCash Collections', this.formatCurrency(eod.digital)],
      ['Total Revenue', this.formatCurrency(eod.total)],
      ['Transaction Count', String(eod.transactions)]
    ];

    return this.writeTable(
      doc,
      y,
      [
        { header: 'Metric', widthRatio: 0.62 },
        { header: 'Value', widthRatio: 0.38, align: 'right' }
      ],
      rows,
      context
    );
  }

  private renderShrinkageReport(doc: import('jspdf').jsPDF, startY: number, context: ReportRenderContext): number {
    let y = this.writeSectionHeading(doc, startY, 'Shrinkage Report (Filtered)');

    const rows = this.filteredShrinkageRows().map((row) => [
      row.product,
      row.sku,
      String(row.systemStock),
      String(row.physicalCount),
      this.formatVariance(row.variance)
    ]);

    if (rows.length === 0) {
      rows.push(['No data for current filters.', '-', '-', '-', '-']);
    }

    y = this.writeTable(
      doc,
      y,
      [
        { header: 'Product', widthRatio: 0.35 },
        { header: 'SKU', widthRatio: 0.16 },
        { header: 'System', widthRatio: 0.15, align: 'right' },
        { header: 'Physical', widthRatio: 0.15, align: 'right' },
        { header: 'Variance', widthRatio: 0.19, align: 'right' }
      ],
      rows,
      context
    );

    return this.writeNote(doc, y, 'Variance = Physical Count - System Stock.', context);
  }

  private renderSalesReport(doc: import('jspdf').jsPDF, startY: number, context: ReportRenderContext): number {
    let y = this.writeSectionHeading(doc, startY, 'Sales Trends (Filtered)');
    const peakSales = this.maxTrendValue();
    const rows = this.filteredSalesTrend().map((slot) => [
      slot.hour,
      this.formatCurrency(slot.sales),
      `${((slot.sales / peakSales) * 100).toFixed(1)}% of peak`
    ]);

    if (rows.length === 0) {
      rows.push(['No data for current filters.', '-', '-']);
    }

    return this.writeTable(
      doc,
      y,
      [
        { header: 'Hour Slot', widthRatio: 0.24 },
        { header: 'Sales Value', widthRatio: 0.33, align: 'right' },
        { header: 'Relative Performance', widthRatio: 0.43, align: 'right' }
      ],
      rows,
      context
    );
  }

  private writeSectionHeading(doc: import('jspdf').jsPDF, startY: number, heading: string): number {
    const y = startY;
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12);
    doc.text(heading, this.reportLayout.left, y);
    return y + 7;
  }

  private writeNote(
    doc: import('jspdf').jsPDF,
    startY: number,
    note: string,
    context: ReportRenderContext
  ): number {
    let y = this.ensureSpace(doc, startY, 6, context);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.setTextColor(95, 95, 95);
    doc.text(note, this.reportLayout.left, y);
    doc.setTextColor(0, 0, 0);
    y += 5;
    return y;
  }

  private writeTable(
    doc: import('jspdf').jsPDF,
    startY: number,
    columns: ReportTableColumn[],
    rows: string[][],
    context: ReportRenderContext
  ): number {
    const pageWidth = doc.internal.pageSize.getWidth();
    const tableWidth = pageWidth - this.reportLayout.left - this.reportLayout.right;
    const headerHeight = 8;
    let y = startY;

    const drawHeader = () => {
      doc.setFillColor(236, 241, 246);
      doc.rect(this.reportLayout.left, y, tableWidth, headerHeight, 'F');
      doc.setDrawColor(190, 190, 190);
      doc.rect(this.reportLayout.left, y, tableWidth, headerHeight);

      let x = this.reportLayout.left;
      for (const column of columns) {
        const width = tableWidth * column.widthRatio;
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(9);
        doc.text(column.header, x + 1.5, y + 5.3);
        doc.line(x + width, y, x + width, y + headerHeight);
        x += width;
      }

      y += headerHeight;
    };

    y = this.ensureSpace(doc, y, headerHeight, context);
    drawHeader();

    for (const row of rows) {
      const cells = columns.map((column, index) => {
        const width = tableWidth * column.widthRatio;
        const value = row[index] ?? '-';
        const wrapped = doc.splitTextToSize(String(value), Math.max(8, width - 3));
        return wrapped.length > 0 ? wrapped : ['-'];
      });

      const lineCount = Math.max(...cells.map((cellLines) => cellLines.length));
      const rowHeight = Math.max(7, lineCount * 3.8 + 2.5);

      if (y + rowHeight > this.reportLayout.contentBottom) {
        y = this.startNewPage(doc, context);
        y = this.ensureSpace(doc, y, headerHeight, context);
        drawHeader();
      }

      let x = this.reportLayout.left;
      for (let i = 0; i < columns.length; i += 1) {
        const column = columns[i];
        const width = tableWidth * column.widthRatio;
        const align = column.align ?? 'left';
        const textX = align === 'right' ? x + width - 1.5 : align === 'center' ? x + width / 2 : x + 1.5;
        const textY = y + 4.2;

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(9);
        doc.rect(x, y, width, rowHeight);
        doc.text(cells[i], textX, textY, { align });

        x += width;
      }

      y += rowHeight;
    }

    return y + 3;
  }

  private ensureSpace(
    doc: import('jspdf').jsPDF,
    currentY: number,
    requiredHeight: number,
    context: ReportRenderContext
  ): number {
    if (currentY + requiredHeight <= this.reportLayout.contentBottom) {
      return currentY;
    }

    return this.startNewPage(doc, context);
  }

  private startNewPage(doc: import('jspdf').jsPDF, context: ReportRenderContext): number {
    doc.addPage();
    this.drawPageFrame(doc, context);
    return this.reportLayout.contentTop;
  }

  private drawPageFrame(doc: import('jspdf').jsPDF, context: ReportRenderContext): void {
    const pageWidth = doc.internal.pageSize.getWidth();
    const pageHeight = doc.internal.pageSize.getHeight();

    if (context.logoDataUrl) {
      doc.addImage(context.logoDataUrl, 'PNG', this.reportLayout.left, this.reportLayout.headerTop, 15, 15);
    } else {
      doc.setFillColor(24, 94, 89);
      doc.roundedRect(this.reportLayout.left, this.reportLayout.headerTop, 15, 15, 2, 2, 'F');
      doc.setTextColor(255, 255, 255);
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8);
      doc.text(this.branding.shortName, this.reportLayout.left + 7.5, this.reportLayout.headerTop + 8.5, { align: 'center' });
      doc.setTextColor(0, 0, 0);
    }

    const topTextX = this.reportLayout.left + 18;
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text(this.branding.name, topTextX, this.reportLayout.headerTop + 5);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8.5);
    doc.text(this.branding.addressLine1, topTextX, this.reportLayout.headerTop + 9.8);
    doc.text(this.branding.addressLine2, topTextX, this.reportLayout.headerTop + 13.8);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12);
    doc.text(context.title, pageWidth - this.reportLayout.right, this.reportLayout.headerTop + 5, { align: 'right' });

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8.5);
    doc.text(`Generated: ${context.generatedAt}`, pageWidth - this.reportLayout.right, this.reportLayout.headerTop + 10.2, { align: 'right' });

    doc.setDrawColor(175, 175, 175);
    doc.line(this.reportLayout.left, this.reportLayout.headerBottom, pageWidth - this.reportLayout.right, this.reportLayout.headerBottom);

    doc.line(this.reportLayout.left, this.reportLayout.footerTop, pageWidth - this.reportLayout.right, this.reportLayout.footerTop);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.text(`${this.branding.email} | ${this.branding.phone}`, this.reportLayout.left, pageHeight - 9);
    doc.text(SYSTEM_BRANDING_FULL_ADDRESS, this.reportLayout.left, pageHeight - 5.2);
    doc.text(`Page ${doc.getNumberOfPages()}`, pageWidth - this.reportLayout.right, pageHeight - 5.2, { align: 'right' });
  }

  private async getLogoDataUrlForPdf(): Promise<string | null> {
    if (this.cachedLogoPngDataUrl !== undefined) {
      return this.cachedLogoPngDataUrl;
    }

    try {
      const response = await fetch(this.branding.logoPath);
      if (!response.ok) {
        throw new Error(`Logo read failed with status ${response.status}`);
      }

      const svgSource = await response.text();
      const svgDataUrl = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svgSource)}`;
      const image = await this.loadImage(svgDataUrl);
      const canvas = document.createElement('canvas');
      canvas.width = 300;
      canvas.height = 300;

      const context2D = canvas.getContext('2d');
      if (!context2D) {
        throw new Error('Unable to create a drawing context for logo conversion.');
      }

      context2D.drawImage(image, 0, 0, canvas.width, canvas.height);
      this.cachedLogoPngDataUrl = canvas.toDataURL('image/png');
      return this.cachedLogoPngDataUrl;
    } catch {
      this.cachedLogoPngDataUrl = null;
      return null;
    }
  }

  private loadImage(source: string): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error('Image failed to load'));
      image.src = source;
    });
  }

  private formatCurrency(value: number): string {
    return `$${value.toFixed(2)}`;
  }

  private formatVariance(value: number): string {
    return value > 0 ? `+${value}` : String(value);
  }

  private getReportTitle(section: SelectedReportSection): string {
    if (section === 'all') {
      return 'Comprehensive System Report';
    }

    if (section === 'eod') {
      return 'EOD Financial Report';
    }

    if (section === 'shrinkage') {
      return 'Inventory Shrinkage Report';
    }

    return 'Sales Trends Report';
  }

  private getReportExplanation(section: SelectedReportSection): string {
    if (section === 'all') {
      return 'Explanation: This document compiles EOD, shrinkage, and sales trend sections using the active report filters.';
    }

    if (section === 'eod') {
      return 'Explanation: This summary consolidates all payment channels for the selected reporting window.';
    }

    if (section === 'shrinkage') {
      return 'Explanation: Negative variance values indicate stock loss and positive values indicate overages.';
    }

    return 'Explanation: Relative performance compares each hour against the highest observed hourly sales value.';
  }
}
