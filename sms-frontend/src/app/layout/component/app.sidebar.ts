import { Component, computed, effect, ElementRef, inject, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, Subject, takeUntil } from 'rxjs';
import { AppMenu } from './app.menu';
import { LayoutService } from '@/app/layout/service/layout.service';

@Component({
    selector: 'app-sidebar',
    standalone: true,
    imports: [AppMenu, RouterModule],
    template: `
        <div class="layout-sidebar" (mouseenter)="onSidebarMouseEnter()" (mouseleave)="onSidebarMouseLeave()">
            <app-menu></app-menu>
        </div>
    `
})
export class AppSidebar implements OnInit, OnDestroy {
    layoutService = inject(LayoutService);

    router = inject(Router);

    el = inject(ElementRef);

    private outsideClickListener: ((event: MouseEvent) => void) | null = null;
    private hoverCloseTimer: ReturnType<typeof setTimeout> | null = null;

    private destroy$ = new Subject<void>();

    constructor() {
        effect(() => {
            const state = this.layoutService.layoutState();

            if (this.layoutService.isDesktop()) {
                if (state.overlayMenuActive) {
                    this.bindOutsideClickListener();
                } else {
                    this.unbindOutsideClickListener();
                }
            } else {
                if (state.mobileMenuActive) {
                    this.bindOutsideClickListener();
                } else {
                    this.unbindOutsideClickListener();
                }
            }
        });
    }

    ngOnInit() {
        this.router.events
            .pipe(
                filter((event) => event instanceof NavigationEnd),
                takeUntil(this.destroy$)
            )
            .subscribe((event) => {
                const navEvent = event as NavigationEnd;
                this.onRouteChange(navEvent.urlAfterRedirects);
            });

        this.onRouteChange(this.router.url);
    }

    ngOnDestroy() {
        this.destroy$.next();
        this.destroy$.complete();
        this.unbindOutsideClickListener();
        this.clearHoverCloseTimer();
    }

    private onRouteChange(path: string) {
        this.layoutService.layoutState.update((val) => ({
            ...val,
            activePath: path,
            overlayMenuActive: false,
            mobileMenuActive: false,
            menuHoverActive: false
        }));
    }

    private bindOutsideClickListener() {
        if (!this.outsideClickListener) {
            this.outsideClickListener = (event: MouseEvent) => {
                if (this.isOutsideClicked(event)) {
                    this.layoutService.layoutState.update((val) => ({
                        ...val,
                        overlayMenuActive: false,
                        mobileMenuActive: false,
                        menuHoverActive: false
                    }));
                }
            };

            document.addEventListener('click', this.outsideClickListener);
        }
    }

    private unbindOutsideClickListener() {
        if (this.outsideClickListener) {
            document.removeEventListener('click', this.outsideClickListener);
            this.outsideClickListener = null;
        }
    }

    private isOutsideClicked(event: MouseEvent): boolean {
        const topbarButtonEl = document.querySelector('.topbar-start > button');
        const sidebarEl = this.el.nativeElement;

        return !(
            sidebarEl?.isSameNode(event.target as Node) ||
            sidebarEl?.contains(event.target as Node) ||
            topbarButtonEl?.isSameNode(event.target as Node) ||
            topbarButtonEl?.contains(event.target as Node)
        );
    }

    onSidebarMouseEnter(): void {
        if (!this.layoutService.isDesktop()) {
            return;
        }

        this.clearHoverCloseTimer();
        this.layoutService.layoutState.update((state) => ({
            ...state,
            menuHoverActive: true
        }));
    }

    onSidebarMouseLeave(): void {
        if (!this.layoutService.isDesktop()) {
            return;
        }

        this.clearHoverCloseTimer();
        this.hoverCloseTimer = setTimeout(() => {
            this.layoutService.layoutState.update((state) => ({
                ...state,
                staticMenuDesktopInactive: true,
                menuHoverActive: false
            }));
        }, 120);
    }

    private clearHoverCloseTimer(): void {
        if (this.hoverCloseTimer) {
            clearTimeout(this.hoverCloseTimer);
            this.hoverCloseTimer = null;
        }
    }
}
