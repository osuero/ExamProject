import { HttpErrorResponse, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { Api, Me } from './api';

@Injectable({ providedIn: 'root' })
export class Auth {
  private api = inject(Api);
  readonly me = signal<Me>({ authenticated: false });
  readonly ready = signal(false);
  readonly isAdmin = computed(() => this.me().role === 'Admin');

  async init() {
    try {
      await this.api.get('/api/auth/csrf');
      this.me.set(await this.api.get<Me>('/api/auth/me'));
    } catch {
      this.me.set({ authenticated: false });
    } finally {
      this.ready.set(true);
    }
  }

  requestLink(email: string) { return this.api.post<{ message: string }>('/api/auth/request-link', { email }); }

  async verify(token: string) {
    const me = await this.api.post<{ id: string; email: string; role: 'Student' | 'Admin' }>('/api/auth/verify', { token });
    this.me.set({ authenticated: true, ...me });
  }

  async logout() {
    await this.api.post('/api/auth/logout');
    this.me.set({ authenticated: false });
  }

  async deleteAccount() {
    await this.api.delete('/api/me');
    await this.api.get('/api/auth/csrf');
    this.me.set({ authenticated: false });
  }
}

/** Refreshes the antiforgery cookie once when the server reports an invalid token (e.g. after the session expired). */
export const csrfRetryInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const api = inject(Api);
  return next(req).pipe(
    catchError((e: unknown) => {
      if (e instanceof HttpErrorResponse && e.status === 403 && e.error?.error === 'csrf_invalid' && !req.headers.has('x-csrf-retried')) {
        return from(api.get('/api/auth/csrf')).pipe(
          switchMap(() => {
            const token = document.cookie.split('; ').find((c) => c.startsWith('XSRF-TOKEN='))?.split('=')[1] ?? '';
            return next(req.clone({ setHeaders: { 'X-XSRF-TOKEN': decodeURIComponent(token), 'x-csrf-retried': '1' } }));
          }),
        );
      }
      return throwError(() => e);
    }),
  );
};

export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(Auth);
  const router = inject(Router);
  if (!auth.ready()) await auth.init();
  return auth.me().authenticated ? true : router.createUrlTree(['/login'], { queryParams: { next: state.url } });
};

export const adminGuard: CanActivateFn = async () => {
  const auth = inject(Auth);
  const router = inject(Router);
  if (!auth.ready()) await auth.init();
  return auth.isAdmin() ? true : router.createUrlTree(['/']);
};
