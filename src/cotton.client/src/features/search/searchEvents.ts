export const OPEN_SEARCH_EVENT = "cotton:open-search";

export const openSearchModal = () => {
  window.dispatchEvent(new Event(OPEN_SEARCH_EVENT));
};
