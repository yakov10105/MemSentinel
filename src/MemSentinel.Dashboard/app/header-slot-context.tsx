"use client";

import { createContext, useContext, useState, type ReactNode } from "react";

const SlotContext = createContext<ReactNode>(null);
const SetSlotContext = createContext<(node: ReactNode) => void>(() => {});

export function HeaderSlotProvider({ children }: { children: ReactNode }) {
  const [slot, setSlot] = useState<ReactNode>(null);
  return (
    <SetSlotContext.Provider value={setSlot}>
      <SlotContext.Provider value={slot}>{children}</SlotContext.Provider>
    </SetSlotContext.Provider>
  );
}

export function useHeaderSlot(): ReactNode {
  return useContext(SlotContext);
}

export function useSetHeaderSlot(): (node: ReactNode) => void {
  return useContext(SetSlotContext);
}
