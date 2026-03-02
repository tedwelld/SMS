import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth.service';

@Component({
  selector: 'app-forgot-password-page',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password-page.component.html',
  styleUrl: './forgot-password-page.component.scss'
})
export class ForgotPasswordPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  readonly submitting = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    usernameOrEmail: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]]
  });

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.message.set(null);
    this.error.set(null);

    try {
      await this.auth.forgotPassword(this.form.getRawValue() as { usernameOrEmail: string; newPassword: string });
      this.message.set('Password reset complete. Use your new password to log in.');
    } catch (error) {
      this.error.set(this.auth.apiErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
