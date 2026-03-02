import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth.service';

@Component({
  selector: 'app-create-account-page',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './create-account-page.component.html',
  styleUrl: './create-account-page.component.scss'
})
export class CreateAccountPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    username: ['', [Validators.required, Validators.minLength(3)]],
    name: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    role: ['Staff', [Validators.required]]
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
      await this.auth.createAccount(this.form.getRawValue() as {
        username: string;
        name: string;
        email: string;
        password: string;
        role: 'Admin' | 'Staff';
      });
      this.message.set('Account created. You can now log in.');
      setTimeout(() => void this.router.navigateByUrl('/auth/login'), 900);
    } catch (error) {
      this.error.set(this.auth.apiErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
