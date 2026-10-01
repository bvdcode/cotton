import { afterEach, expect, it, vi } from "vitest";
import { httpClient } from "./httpClient";
import { lookupSiblingNames, SIBLING_NAME_BATCH_SIZE } from "./siblingNames";
import { siblingNameLookupSchema } from "./schemas/node";
import { z } from "zod";

afterEach(() => vi.restoreAllMocks());
const requestSchema = z.object({
  names: z.array(z.string()),
  includeTakenNamesOnConflict: z.boolean(),
});

it("batches more than 20000 names and keeps duplicate names together for rename conflicts", async () => {
  const requests: Array<z.infer<typeof requestSchema>> = [];
  const post = vi
    .spyOn(httpClient, "post")
    .mockImplementation(async (_, body) => {
      const request = requestSchema.parse(body);
      requests.push(request);
      const duplicates = new Set(request.names).size < request.names.length;
      return {
        data: siblingNameLookupSchema.parse({
          nodes: [],
          files: [],
          takenNameKeys:
            duplicates && request.includeTakenNamesOnConflict
              ? ["photo (1).jpg"]
              : [],
        }),
      };
    });
  const names = [
    "photo.jpg",
    ...Array.from({ length: 20001 }, (_, i) => `file-${i}`),
    "photo.jpg",
  ];
  const result = await lookupSiblingNames("folder-1", names, true);
  expect(post).toHaveBeenCalledTimes(
    Math.ceil(names.length / SIBLING_NAME_BATCH_SIZE),
  );
  expect(
    requests.every(
      (request) => request.names.length <= SIBLING_NAME_BATCH_SIZE,
    ),
  ).toBe(true);
  expect(new Set(requests.flatMap((request) => request.names))).toEqual(
    new Set(names),
  );
  expect(requests[0].names.filter((name) => name === "photo.jpg")).toHaveLength(
    2,
  );
  expect(
    requests.slice(1).every((request) => !request.includeTakenNamesOnConflict),
  ).toBe(true);
  expect(result.takenNameKeys).toEqual(["photo (1).jpg"]);
});
