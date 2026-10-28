import {
  graphql,
  useQueryLoader,
  usePreloadedQuery,
  type PreloadedQuery,
} from "react-relay";
import { useEffect, useState } from "react";
import type { HelloWorldQuery } from "./__generated__/HelloWorldQuery.graphql";

const helloQuery2 = graphql`
  query HelloWorldQuery {
    helloWorld {
      hello
      timestamp
    }
  }
`;

function Content({ queryRef }: { queryRef: PreloadedQuery<HelloWorldQuery> }) {
  const data = usePreloadedQuery<HelloWorldQuery>(helloQuery2, queryRef);

  return (
    <div id="data-output">
      <p>Hello: {data?.helloWorld?.hello}</p>
      <p>Timestamp: {data?.helloWorld?.timestamp}</p>
    </div>
  );
}

function DataDisplayPublic({ isReloadable }: { isReloadable?: boolean }) {
  isReloadable ??= true;
  const [queryRef, loadQuery, disposeQueryFn] = useQueryLoader<HelloWorldQuery>(helloQuery2);
  const [isFetching, setIsFetching] = useState(false);

  useEffect(() => {
    loadQuery({}, { fetchPolicy: "store-or-network" });

    return () => {
      disposeQueryFn();
    };
  }, [loadQuery, disposeQueryFn]);

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
        {isReloadable && (
          <button onClick={handleRefetch} disabled={isFetching}>
            {isFetching ? "Fetching..." : "Fetch Data"}
          </button>
        )}
        {queryRef && <Content queryRef={queryRef} />}
        <hr />
      </div>
    </>
  );
}

export default DataDisplayPublic;
