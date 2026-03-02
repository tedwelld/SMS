import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/auth/login']);
  }

  const expectedRole = route.data['role'] as 'admin' | 'staff' | undefined;
  if (!expectedRole) {
    return true;
  }

  if (auth.role !== expectedRole) {
    const fallback = auth.role === 'admin' ? '/dashboard' : '/pos';
    return router.createUrlTree([fallback]);
  }

  return true;
};
