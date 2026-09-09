import { HttpContextToken, HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, from, switchMap, throwError } from "rxjs";
import { AuthState } from "../auth/auth-state.service";
import { SessionService } from "../auth/session.service";
import { RUNTIME_CONFIG } from "../config/runtime-config";

const retried = new HttpContextToken<boolean>(() => false);

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const config = inject(RUNTIME_CONFIG);
  if (!request.url.startsWith(`${config.identityBaseUrl}/api/v1/admin`))
    return next(request);
  const auth = inject(AuthState);
  const session = inject(SessionService);
  const send = (token: string | null) =>
    next(
      token
        ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
        : request,
    );
  const refreshFirst = auth.expiresWithin(30);
  const initial = refreshFirst
    ? from(session.refreshSession()).pipe(switchMap(send))
    : send(auth.token());
  return initial.pipe(
    catchError((error: unknown) => {
      const status = (error as { status?: number }).status;
      if (status !== 401 || refreshFirst || request.context.get(retried))
        return throwError(() => error);
      return from(session.refreshSession()).pipe(
        switchMap((token) =>
          token
            ? next(
                request.clone({
                  context: request.context.set(retried, true),
                  setHeaders: { Authorization: `Bearer ${token}` },
                }),
              )
            : throwError(() => error),
        ),
      );
    }),
  );
};
