using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Yvonta
{
    public class EgoLinkLanguageDetector
    {
        private Dictionary<string, HashSet<string>> languageStopwords = new Dictionary<string, HashSet<string>>();

        public EgoLinkLanguageDetector()
        {
            InitializeStopwords();
        }

        private void InitializeStopwords()
        {
            // English (~50 core words)
            languageStopwords.Add("en", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "the", "and", "that", "have", "for", "not", "with", "you", "this", "but", "his", "from", "they",
                "we", "say", "her", "she", "or", "an", "will", "my", "one", "all", "would", "there", "their",
                "what", "so", "up", "out", "if", "about", "who", "get", "which", "go", "me", "when", "make",
                "can", "like", "time", "no", "just", "him", "know", "take", "people", "into", "year", "your"
            });

            // Spanish
            languageStopwords.Add("es", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "el", "la", "los", "las", "un", "una", "unos", "unas", "por", "para", "con", "que", "como", 
                "mas", "pero", "este", "esta", "estos", "estas", "como", "cuando", "donde", "quien", "porque",
                "del", "al", "sus", "mas", "como", "sin", "sobre", "este", "entre", "hasta", "desde", "todos",
                "tambien", "me", "hasta", "hay", "donde", "quien", "nos", "durante", "todos", "uno", "les"
            });

            // French
            languageStopwords.Add("fr", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "le", "la", "les", "un", "une", "des", "pour", "dans", "avec", "est", "que", "pas", "plus", 
                "sur", "par", "ce", "cette", "ces", "qui", "ne", "en", "du", "de", "aux", "au", "elle", "il", 
                "nous", "vous", "ils", "elles", "mais", "ou", "donc", "car", "ni", "si", "comme", "quand", 
                "tout", "tous", "faire", "plusieurs", "meme", "autre", "autres"
            });
                
            // German
            languageStopwords.Add("de", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "der", "die", "das", "und", "ist", "zu", "den", "von", "mit", "auf", "für", "ein", "eine", 
                "einen", "einer", "eines", "einem", "im", "nicht", "dem", "des", "sich", "auch", "als", 
                "nach", "wie", "für", "oder", "aber", "bei", "wir", "ihr", "sie", "aus", "durch", "über", 
                "haben", "werden", "kann", "nur", "einen", "sind", "oder", "welche", "dass", "daß"
            });
        
            // Dutch
            languageStopwords.Add("nl", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "het", "een", "van", "ik", "te", "dat", "op", "met", "voor", "niet", "zijn", "om", "ook", 
                "als", "over", "door", "aan", "bij", "uit", "is", "maar", "gewoon", "geen", "wel", "ons", 
                "onze", "hier", "daar", "waar", "hoe", "wat", "wie", "waarom", "worden", "heeft", "hebben", 
                "kunnen", "moeten", "zo", "nog", "reeds", "toch"
            });

            // Tagalog / Filipino
            languageStopwords.Add("tl", new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "ang", "mga", "sa", "ng", "na", "at", "ako", "ikaw", "siya", "kami", "tayo", "sila", "ito", 
                "iyon", "dito", "doon", "hindi", "din", "rin", "para", "dahil", "nang", "kay", "kanya", 
                "sakanila", "pa", "ba", "po", "opo", "nga", "naman", "pala", "kasi", "sino", "ano", "paano", 
                "bakit", "kailan", "saan", "at", "upang", "habang", "maging", "kundi"
            });
        }

        public string DetectLanguage(string text, string defaultFallback = "en")
        {
            if (string.IsNullOrWhiteSpace(text)) return defaultFallback;

            string cleanText = Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\s]", "");
            string[] words = cleanText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return defaultFallback;

            Dictionary<string, float> languageScores = new Dictionary<string, float>();
            foreach (string lang in languageStopwords.Keys)
            {
                languageScores[lang] = 0f;
            }

            // --- Character & Pattern Heuristics ---

            // German unique characters/combinations
            if (Regex.IsMatch(cleanText, @"[ß]")) languageScores["de"] += 3.0f;
            if (Regex.IsMatch(cleanText, @"[äöü]")) languageScores["de"] += 1.5f;

            // Dutch specific 'ij' digraph (e.g. 'vrij', 'krijgen', 'blij')
            if (Regex.IsMatch(cleanText, @"\b\w*ij\w*\b")) languageScores["nl"] += 1.5f;

            // French unique accents and ligatures
            if (Regex.IsMatch(cleanText, @"[çœæèêëàâùû]")) languageScores["fr"] += 2.0f;

            // Spanish upside-down punctuation or accents
            if (Regex.IsMatch(text, @"[¿¡]")) languageScores["es"] += 3.0f;
            if (Regex.IsMatch(cleanText, @"[áíóú]")) languageScores["es"] += 1.0f;

            // Shared 'ñ' between Spanish and Tagalog
            if (Regex.IsMatch(cleanText, @"[ñ]")) 
            {
                languageScores["es"] += 1.0f;
                languageScores["tl"] += 1.0f;
            }

            // Tagalog stand-alone particle "ng" and "mga"
            if (Regex.IsMatch(cleanText, @"\bng\b")) languageScores["tl"] += 2.0f;
            if (Regex.IsMatch(cleanText, @"\bmga\b")) languageScores["tl"] += 2.0f;

            // --- Stopword Matching ---
            foreach (string word in words)
            {
                foreach (var kvp in languageStopwords)
                {
                    if (kvp.Value.Contains(word))
                    {
                        languageScores[kvp.Key] += 1.0f;
                    }
                }
            }

            // Determine best match
            string bestLanguage = defaultFallback;
            float highestScore = 0f;

            foreach (var score in languageScores)
            {
                if (score.Value > highestScore)
                {
                    highestScore = score.Value;
                    bestLanguage = score.Key;
                }
            }

            return highestScore > 0.5f ? bestLanguage : defaultFallback;
        }
    }
}