import { useEffect } from "react";

interface FaviconManagerProps {
  faviconUrl: string;
}

const FaviconManager: React.FC<FaviconManagerProps> = ({ faviconUrl }) => {
  useEffect(() => {
    let link = document.querySelector("link[rel~='icon']") as HTMLLinkElement;
    if (!link) {
      link = document.createElement("link");
      link.rel = "icon";
      document.head.appendChild(link);
    }
    link.href = faviconUrl;
  }, [faviconUrl]);

  return null;
};

export default FaviconManager;
