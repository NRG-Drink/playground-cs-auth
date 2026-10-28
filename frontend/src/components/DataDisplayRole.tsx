import {
  graphql,
  usePreloadedQuery,
  useQueryLoader,
  type PreloadedQuery,
} from "react-relay";
import type { HelloAdminQuery } from "./__generated__/HelloAdminQuery.graphql";
import { useAuth } from "../auth/context";
import { AuthorizationError } from "../auth/errors";
import { ErrorBoundary, type FallbackProps } from "react-error-boundary";
import { useEffect, useState } from "react";

const helloAdminQuery = graphql`
  query HelloAdminQuery {
    helloAdmin {
      hello
      timestamp
    }
  }
`;

function Content({ queryRef }: { queryRef: PreloadedQuery<HelloAdminQuery> }) {
  const data = usePreloadedQuery<HelloAdminQuery>(helloAdminQuery, queryRef);

  return (
    <div id="data-output">
      <p>Hello: {data.helloAdmin.hello}</p>
      <p>Timestamp: {data.helloAdmin.timestamp}</p>
    </div>
  );
}

function RoleQueryFallback({ error }: FallbackProps) {
  const message =
    error instanceof AuthorizationError
      ? "Your account does not have the Admin role."
      : "The Admin-only data could not be loaded. Check your access and try again.";

  return <p className="role-notice" role="alert">{message}</p>;
}

function DataDisplayRole({ isReloadable }: { isReloadable?: boolean }) {
  isReloadable ??= true;
  const { isLoggedIn, hasRole } = useAuth();
  const canViewAdminData = isLoggedIn && hasRole("Admin");
  const [queryRef, loadQuery, disposeQueryFn] =
    useQueryLoader<HelloAdminQuery>(helloAdminQuery);
  const [isFetching, setIsFetching] = useState(false);

  useEffect(() => {
    if (canViewAdminData) {
      loadQuery({}, { fetchPolicy: "store-and-network" });
    } else {
      disposeQueryFn();
    }

    return () => {
      disposeQueryFn();
    };
  }, [canViewAdminData, loadQuery, disposeQueryFn]);

  const handleRefetch = () => {
    if (!queryRef) {
      return;
    }

    setIsFetching(true);
    loadQuery({}, { fetchPolicy: "network-only" });
    setIsFetching(false);
  };

  return (
    <>
      <div>
        <hr />
        {canViewAdminData && isReloadable && queryRef && (
          <button onClick={handleRefetch} disabled={isFetching}>
            {isFetching ? "Fetching..." : "Fetch Data"}
          </button>
        )}
        {canViewAdminData && queryRef && (
          <ErrorBoundary
            FallbackComponent={RoleQueryFallback}
            resetKeys={[queryRef]}
          >
            <Content queryRef={queryRef} />
          </ErrorBoundary>
        )}
        {!isLoggedIn && <p>Please sign in as an Admin to view this data.</p>}
        {isLoggedIn && !canViewAdminData && (
          <p className="role-notice">Admin role required.</p>
        )}
        <hr />
      </div>
    </>
  );
}

export default DataDisplayRole;
