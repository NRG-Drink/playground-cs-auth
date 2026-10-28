import { type ReactNode, useState } from "react";
import { ThemeContext } from "./ThemeContext";

export interface ThemeContextType {
    theme: { index: number, text: string, emoji: string };
    toggleTheme: () => void;
}

const defaultIndex = 0;
const themes = [
    { index: 0, text: "light", emoji: "🌞"},
    { index: 1, text: "dark", emoji: "🌚"}
]

export function ThemeProvider({ children }: { children: ReactNode }) {
    const [theme, setTheme] = useState(themes[defaultIndex]);
    const toggleTheme = () => setTheme(prevTheme => {
        const nextIndex = (prevTheme.index + 1) % themes.length;
        return themes[nextIndex];
    });

    return (
        <ThemeContext.Provider value={{ theme, toggleTheme }}>
            {children}
        </ThemeContext.Provider>
    );
}

