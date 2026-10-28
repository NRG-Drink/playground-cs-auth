import { useTheme } from "./useTheme";
import "./ThemeButton.css";

export function ThemeButton() {
  const { theme, toggleTheme } = useTheme();

  return (
    <button
      type="button"
      onClick={toggleTheme}
      className="theme-button"
    >
      <span aria-hidden="true">{theme.emoji}</span>
      <span>{theme.text}</span>
    </button>
  );
}
