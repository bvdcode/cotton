import { Outlet } from "react-router-dom";

export const PublicLayout = () => {
  return (
    <div
      style={{
        width: "100%",
        height: "100%",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      <Outlet />
    </div>
  );
};
