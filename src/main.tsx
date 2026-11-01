import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./ui/App";
import { AvatarGallery } from "./ui/screens/AvatarGallery";
import "./ui/styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    {new URLSearchParams(location.search).has("gallery") ? <AvatarGallery /> : <App />}
  </StrictMode>,
);
