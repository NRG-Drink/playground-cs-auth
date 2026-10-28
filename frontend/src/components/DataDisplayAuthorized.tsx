import {
  graphql,
  usePreloadedQuery,
  useQueryLoader,
  type PreloadedQuery,
} from "react-relay";
import type { HelloUserQuery } from "./__generated__/HelloUserQuery.graphql";
import { useAuth } from "../auth/context";
import { useEffect, useState } from "react";

const helloUserQuery = graphql`
  query HelloUserQuery {
    helloUser {
      hello
      timestamp
    }
  }
`;

function Content({ queryRef }: { queryRef: PreloadedQuery<HelloUserQuery> }) {
  const data = usePreloadedQuery<HelloUserQuery>(helloUserQuery, queryRef);

  return (
    <div id="data-output">
      <p>Hello: {data.helloUser.hello}</p>
      <p>Timestamp: {data.helloUser.timestamp}</p>
    </div>
  );
}

function DataDisplayAuthorized({ isReloadable }: { isReloadable?: boolean }) {
  isReloadable ??= true;
  const { isLoggedIn } = useAuth();
  const [queryRef, loadQuery, disposeQueryFn] =
    useQueryLoader<HelloUserQuery>(helloUserQuery);
  const [isFetching, setIsFetching] = useState(false);

  useEffect(() => {
    if (isLoggedIn) {
      loadQuery({}, { fetchPolicy: "store-and-network" });
    } else {
      disposeQueryFn();
    }

    return () => {
      disposeQueryFn();
    };
  }, [isLoggedIn, loadQuery, disposeQueryFn]);

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
        {/* <h3>Data Display Reload</h3> */}
        {isReloadable && queryRef && (
          <button onClick={handleRefetch} disabled={isFetching}>
            {isFetching ? "Fetching..." : "Fetch Data"}
          </button>
        )}
        {queryRef && <Content queryRef={queryRef} />}
        {!isLoggedIn && <p>Please sign in to view the data.</p>}
        <hr />
      </div>
    </>
  );
}

export default DataDisplayAuthorized;
