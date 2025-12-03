const ProtectedRoute: React.FC = ({
  children,
}: {
  children?: React.ReactNode;
}) => {
  return <>{children}</>;
};

export default ProtectedRoute;
