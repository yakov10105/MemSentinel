const USE_MOCKS = process.env.NEXT_PUBLIC_USE_MOCKS === "true";

export function useMockFlag(): boolean {
  return USE_MOCKS;
}
