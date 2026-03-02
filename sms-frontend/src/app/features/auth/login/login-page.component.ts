import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth.service';

@Component({
  selector: 'app-login-page',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss'
})
export class LoginPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    usernameOrEmail: ['', [Validators.required]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    role: ['admin', [Validators.required]]
  });

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    try {
      const { usernameOrEmail, password, role } = this.form.getRawValue();
      const session = await this.auth.login({
        usernameOrEmail: usernameOrEmail ?? '',
        password: password ?? '',
        role: role === 'staff' ? 'staff' : 'admin'
      });

      void this.router.navigateByUrl(session.role === 'admin' ? '/dashboard' : '/pos');
    } catch (error) {
      this.error.set(this.auth.apiErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
