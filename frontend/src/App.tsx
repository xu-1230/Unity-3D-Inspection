import ErrorBoundary from './components/ErrorBoundary';
import DashboardPage from './pages/DashboardPage';

export default function App() {
  return (
    <ErrorBoundary>
      <DashboardPage />
    </ErrorBoundary>
  );
}
