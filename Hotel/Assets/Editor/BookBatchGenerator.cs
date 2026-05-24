// Assets/Editor/BookBatchGenerator.cs
// ═══════════════════════════════════════════════════════════════════════════
// ВИКОРИСТАННЯ:
//   Bookstore → 📚 Book Batch Generator
//
// WORKFLOW:
//   1. Вставити список BookEntry[] з BookData_PublicDomain.cs у BookData нижче
//   2. У вікні призначити 16 префабів (Book_Color_00..15)
//   3. Вибрати Output Path
//   4. Натиснути "Генерувати"
//
// СТРУКТУРА ПАПОК:
//   Assets/Data/Books/Generated/
//     Classic/
//     Fantasy/
//     SciFi/
//     Horror/
//     Mystery/
//     Biography/
//     Academic/
//
// ═══════════════════════════════════════════════════════════════════════════


using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class BookBatchGenerator : EditorWindow
{
    #if UNITY_EDITOR
    // ── Меню ─────────────────────────────────────────────────────────────
    [MenuItem("Bookstore/📚 Book Batch Generator")]
    public static void ShowWindow() => GetWindow<BookBatchGenerator>("Book Generator");

    // ── Налаштування (серіалізуються між сесіями через EditorPrefs) ──────
    private BookDatabase _database;
    private string       _outputPath = "Assets/Data/Books/Generated";
    private bool         _clearExisting  = false;
    private bool         _addToDatabase  = true;
    private bool         _showPrefabs    = true;
    private bool         _showStats      = true;
    private Vector2      _scroll;

    // 16 префабів: colorIndex → GameObject
    private readonly GameObject[] _colorPrefabs = new GameObject[16];

    // ── Константи ────────────────────────────────────────────────────────
    private const string PrefKey = "BBG_";

    // ─────────────────────────────────────────────────────────────────────
    // DATA — вставляй масив книг сюди
    // (або підключай зовнішній статичний клас)
    // ─────────────────────────────────────────────────────────────────────

    // ── BookEntry: структура одного рядка ─────────────────────────────
    [System.Serializable]
    public struct BookEntry
    {
        public string     title;
        public string     author;
        public BookGenre  genre;
        public int        year;
        public BookRarity rarity;

        public BookEntry(string t, string a, BookGenre g, int y, BookRarity r)
        {
            title  = t; author = a; genre = g; year = y; rarity = r;
        }
    }

    // ── СПИСОК КНИГ — вставляй сюди масив з BookData_PublicDomain.cs ──
    private static readonly BookEntry[] BookData = new BookEntry[]
    {
        // ↓↓↓ ВСТАВИТИ СЮДИ ВМІСТ З BookData_PublicDomain.cs ↓↓↓
        // Приклад:
        // ╔══════════════════════════════════════════════════════════╗
    // ║  CLASSIC — 100 творів                                   ║
    // ╚══════════════════════════════════════════════════════════╝

    new BookEntry("Pride and Prejudice",                    "Jane Austen",           BookGenre.Classic, 1813, BookRarity.Common),
    new BookEntry("Sense and Sensibility",                  "Jane Austen",           BookGenre.Classic, 1811, BookRarity.Common),
    new BookEntry("Emma",                                   "Jane Austen",           BookGenre.Classic, 1815, BookRarity.Common),
    new BookEntry("Persuasion",                             "Jane Austen",           BookGenre.Classic, 1817, BookRarity.Uncommon),
    new BookEntry("Northanger Abbey",                       "Jane Austen",           BookGenre.Classic, 1817, BookRarity.Common),
    new BookEntry("Mansfield Park",                         "Jane Austen",           BookGenre.Classic, 1814, BookRarity.Common),
    new BookEntry("Jane Eyre",                              "Charlotte Brontë",      BookGenre.Classic, 1847, BookRarity.Rare),
    new BookEntry("Wuthering Heights",                      "Emily Brontë",          BookGenre.Classic, 1847, BookRarity.Rare),
    new BookEntry("The Tenant of Wildfell Hall",            "Anne Brontë",           BookGenre.Classic, 1848, BookRarity.Uncommon),
    new BookEntry("Great Expectations",                     "Charles Dickens",       BookGenre.Classic, 1861, BookRarity.Rare),
    new BookEntry("Oliver Twist",                           "Charles Dickens",       BookGenre.Classic, 1839, BookRarity.Common),
    new BookEntry("David Copperfield",                      "Charles Dickens",       BookGenre.Classic, 1850, BookRarity.Uncommon),
    new BookEntry("A Tale of Two Cities",                   "Charles Dickens",       BookGenre.Classic, 1859, BookRarity.Common),
    new BookEntry("Bleak House",                            "Charles Dickens",       BookGenre.Classic, 1853, BookRarity.Uncommon),
    new BookEntry("Dombey and Son",                         "Charles Dickens",       BookGenre.Classic, 1848, BookRarity.Uncommon),
    new BookEntry("Boryslav Is Laughing", "Ivan Franko", BookGenre.Classic, 1882, BookRarity.Legendary),
    new BookEntry("The Stolen Happiness", "Ivan Franko", BookGenre.Classic, 1893, BookRarity.Rare),
    new BookEntry("Zakhar Berkut", "Ivan Franko", BookGenre.Classic, 1883, BookRarity.Uncommon),
    new BookEntry("The Fox Mykyta", "Ivan Franko", BookGenre.Classic, 1890, BookRarity.Common),
    new BookEntry("Do We Not Do Evil", "Panas Myrnyi", BookGenre.Classic, 1877, BookRarity.Rare),
    new BookEntry("The Roaming Apostle", "Panas Myrnyi", BookGenre.Classic, 1883, BookRarity.Legendary),
    new BookEntry("The Whirlwind", "Panas Myrnyi", BookGenre.Classic, 1900, BookRarity.Rare),
    new BookEntry("Mykola Dzheria", "Ivan Nechui-Levytsky", BookGenre.Classic, 1878, BookRarity.Uncommon),
    new BookEntry("Kaidasheva Simya", "Ivan Nechui-Levytsky", BookGenre.Classic, 1879, BookRarity.Uncommon),
    new BookEntry("Les Misérables",                         "Victor Hugo",           BookGenre.Classic, 1862, BookRarity.Legendary),
    new BookEntry("The Hunchback of Notre-Dame",            "Victor Hugo",           BookGenre.Classic, 1831, BookRarity.Rare),
    new BookEntry("Toilers of the Sea",                     "Victor Hugo",           BookGenre.Classic, 1866, BookRarity.Uncommon),
    new BookEntry("Don Quixote",                            "Miguel de Cervantes",   BookGenre.Classic, 1605, BookRarity.Legendary),
    new BookEntry("The Count of Monte Cristo",              "Alexandre Dumas",       BookGenre.Classic, 1844, BookRarity.Legendary),
    new BookEntry("The Three Musketeers",                   "Alexandre Dumas",       BookGenre.Classic, 1844, BookRarity.Rare),
    new BookEntry("Twenty Years After",                     "Alexandre Dumas",       BookGenre.Classic, 1845, BookRarity.Uncommon),
    new BookEntry("The Vicomte de Bragelonne",              "Alexandre Dumas",       BookGenre.Classic, 1847, BookRarity.Uncommon),
    new BookEntry("Madame Bovary",                          "Gustave Flaubert",      BookGenre.Classic, 1857, BookRarity.Rare),
    new BookEntry("Sentimental Education",                  "Gustave Flaubert",      BookGenre.Classic, 1869, BookRarity.Uncommon),
    new BookEntry("Moby-Dick",                              "Herman Melville",       BookGenre.Classic, 1851, BookRarity.Rare),
    new BookEntry("Billy Budd",                             "Herman Melville",       BookGenre.Classic, 1924, BookRarity.Uncommon),
    new BookEntry("The Scarlet Letter",                     "Nathaniel Hawthorne",   BookGenre.Classic, 1850, BookRarity.Uncommon),
    new BookEntry("The House of Seven Gables",              "Nathaniel Hawthorne",   BookGenre.Classic, 1851, BookRarity.Uncommon),
    new BookEntry("Adventures of Huckleberry Finn",         "Mark Twain",            BookGenre.Classic, 1884, BookRarity.Rare),
    new BookEntry("The Adventures of Tom Sawyer",           "Mark Twain",            BookGenre.Classic, 1876, BookRarity.Common),
    new BookEntry("A Connecticut Yankee in King Arthur",    "Mark Twain",            BookGenre.Classic, 1889, BookRarity.Common),
    new BookEntry("The Prince and the Pauper",              "Mark Twain",            BookGenre.Classic, 1881, BookRarity.Common),
    new BookEntry("The Great Gatsby",                       "F. Scott Fitzgerald",   BookGenre.Classic, 1925, BookRarity.Uncommon),
    new BookEntry("This Side of Paradise",                  "F. Scott Fitzgerald",   BookGenre.Classic, 1920, BookRarity.Common),
    new BookEntry("The Beautiful and Damned",               "F. Scott Fitzgerald",   BookGenre.Classic, 1922, BookRarity.Common),
    new BookEntry("Ethan Frome",                            "Edith Wharton",         BookGenre.Classic, 1911, BookRarity.Uncommon),
    new BookEntry("The House of Mirth",                     "Edith Wharton",         BookGenre.Classic, 1905, BookRarity.Uncommon),
    new BookEntry("The Age of Innocence",                   "Edith Wharton",         BookGenre.Classic, 1920, BookRarity.Rare),
    new BookEntry("Sister Carrie",                          "Theodore Dreiser",      BookGenre.Classic, 1900, BookRarity.Common),
    new BookEntry("The Red Badge of Courage",               "Stephen Crane",         BookGenre.Classic, 1895, BookRarity.Common),
    new BookEntry("The Call of the Wild",                   "Jack London",           BookGenre.Classic, 1903, BookRarity.Common),
    new BookEntry("White Fang",                             "Jack London",           BookGenre.Classic, 1906, BookRarity.Common),
    new BookEntry("The Sea-Wolf",                           "Jack London",           BookGenre.Classic, 1904, BookRarity.Common),
    new BookEntry("Martin Eden",                            "Jack London",           BookGenre.Classic, 1909, BookRarity.Uncommon),
    new BookEntry("O Pioneers!",                            "Willa Cather",          BookGenre.Classic, 1913, BookRarity.Common),
    new BookEntry("My Ántonia",                             "Willa Cather",          BookGenre.Classic, 1918, BookRarity.Common),
    new BookEntry("The Song of the Lark",                   "Willa Cather",          BookGenre.Classic, 1915, BookRarity.Common),
    new BookEntry("The Awakening",                          "Kate Chopin",           BookGenre.Classic, 1899, BookRarity.Uncommon),
    new BookEntry("Tess of the d'Urbervilles",              "Thomas Hardy",          BookGenre.Classic, 1891, BookRarity.Rare),
    new BookEntry("Far from the Madding Crowd",             "Thomas Hardy",          BookGenre.Classic, 1874, BookRarity.Uncommon),
    new BookEntry("Jude the Obscure",                       "Thomas Hardy",          BookGenre.Classic, 1895, BookRarity.Uncommon),
    new BookEntry("The Return of the Native",               "Thomas Hardy",          BookGenre.Classic, 1878, BookRarity.Common),
    new BookEntry("The Mayor of Casterbridge",              "Thomas Hardy",          BookGenre.Classic, 1886, BookRarity.Common),
    new BookEntry("Middlemarch",                            "George Eliot",          BookGenre.Classic, 1871, BookRarity.Legendary),
    new BookEntry("The Mill on the Floss",                  "George Eliot",          BookGenre.Classic, 1860, BookRarity.Uncommon),
    new BookEntry("Silas Marner",                           "George Eliot",          BookGenre.Classic, 1861, BookRarity.Common),
    new BookEntry("Adam Bede",                              "George Eliot",          BookGenre.Classic, 1859, BookRarity.Common),
    new BookEntry("Vanity Fair",                            "William Makepeace Thackeray", BookGenre.Classic, 1848, BookRarity.Rare),
    new BookEntry("Barry Lyndon",                           "William Makepeace Thackeray", BookGenre.Classic, 1844, BookRarity.Uncommon),
    new BookEntry("The Forest Song", "Lesia Ukrainka", BookGenre.Classic, 1911, BookRarity.Uncommon),
    new BookEntry("The Stone Host", "Lesia Ukrainka", BookGenre.Classic, 1912, BookRarity.Common),
    new BookEntry("Cassandra", "Lesia Ukrainka", BookGenre.Classic, 1903, BookRarity.Common),
    new BookEntry("Fata Morgana", "Mykhailo Kotsiubynsky", BookGenre.Classic, 1910, BookRarity.Common),
    new BookEntry("Shadows of Forgotten Ancestors", "Mykhailo Kotsiubynsky", BookGenre.Classic, 1913, BookRarity.Rare),
    new BookEntry("Marusia", "Hryhorii Kvitka-Osnovianenko", BookGenre.Classic, 1833, BookRarity.Uncommon),
    new BookEntry("The Kobzar", "Taras Shevchenko", BookGenre.Classic, 1840, BookRarity.Legendary),
    new BookEntry("Do Svitu", "Ivan Franko", BookGenre.Classic, 1880, BookRarity.Common),
    new BookEntry("Germinal",                               "Émile Zola",            BookGenre.Classic, 1885, BookRarity.Rare),
    new BookEntry("Nana",                                   "Émile Zola",            BookGenre.Classic, 1880, BookRarity.Uncommon),
    new BookEntry("The Masterpiece",                        "Émile Zola",            BookGenre.Classic, 1886, BookRarity.Uncommon),
    new BookEntry("Thérèse Raquin",                        "Émile Zola",            BookGenre.Classic, 1867, BookRarity.Uncommon),
    new BookEntry("The Red and the Black",                  "Stendhal",              BookGenre.Classic, 1830, BookRarity.Rare),
    new BookEntry("The Charterhouse of Parma",              "Stendhal",              BookGenre.Classic, 1839, BookRarity.Uncommon),
    new BookEntry("Eugénie Grandet",                       "Honoré de Balzac",      BookGenre.Classic, 1833, BookRarity.Common),
    new BookEntry("Père Goriot",                           "Honoré de Balzac",      BookGenre.Classic, 1835, BookRarity.Uncommon),
    new BookEntry("The Wild Ass's Skin",                    "Honoré de Balzac",      BookGenre.Classic, 1831, BookRarity.Uncommon),
    new BookEntry("The Pickwick Papers",                    "Charles Dickens",       BookGenre.Classic, 1837, BookRarity.Common),
    new BookEntry("Nicholas Nickleby",                      "Charles Dickens",       BookGenre.Classic, 1839, BookRarity.Common),
    new BookEntry("Treasure Island",                        "Robert Louis Stevenson",BookGenre.Classic, 1883, BookRarity.Common),
    new BookEntry("Kidnapped",                              "Robert Louis Stevenson",BookGenre.Classic, 1886, BookRarity.Common),
    new BookEntry("The Black Arrow",                        "Robert Louis Stevenson",BookGenre.Classic, 1888, BookRarity.Common),
    new BookEntry("Ivanhoe",                                "Walter Scott",          BookGenre.Classic, 1820, BookRarity.Uncommon),
    new BookEntry("Rob Roy",                                "Walter Scott",          BookGenre.Classic, 1817, BookRarity.Common),
    new BookEntry("Waverley",                               "Walter Scott",          BookGenre.Classic, 1814, BookRarity.Common),
    new BookEntry("The Last of the Mohicans",               "James Fenimore Cooper", BookGenre.Classic, 1826, BookRarity.Common),
    new BookEntry("The Deerslayer",                         "James Fenimore Cooper", BookGenre.Classic, 1841, BookRarity.Common),
    new BookEntry("Main Street",                            "Sinclair Lewis",        BookGenre.Classic, 1920, BookRarity.Common),
    new BookEntry("Babbitt",                                "Sinclair Lewis",        BookGenre.Classic, 1922, BookRarity.Common),
    new BookEntry("The Awakening",                          "Kate Chopin",           BookGenre.Classic, 1899, BookRarity.Uncommon),
    new BookEntry("The Portrait of a Lady",                 "Henry James",           BookGenre.Classic, 1881, BookRarity.Rare),

    // ╔══════════════════════════════════════════════════════════╗
    // ║  FANTASY — 100 творів                                   ║
    // ╚══════════════════════════════════════════════════════════╝

    new BookEntry("Alice's Adventures in Wonderland",       "Lewis Carroll",         BookGenre.Fantasy, 1865, BookRarity.Rare),
    new BookEntry("Through the Looking-Glass",              "Lewis Carroll",         BookGenre.Fantasy, 1871, BookRarity.Rare),
    new BookEntry("The Wonderful Wizard of Oz",             "L. Frank Baum",         BookGenre.Fantasy, 1900, BookRarity.Common),
    new BookEntry("The Marvelous Land of Oz",               "L. Frank Baum",         BookGenre.Fantasy, 1904, BookRarity.Common),
    new BookEntry("Ozma of Oz",                             "L. Frank Baum",         BookGenre.Fantasy, 1907, BookRarity.Common),
    new BookEntry("Dorothy and the Wizard in Oz",           "L. Frank Baum",         BookGenre.Fantasy, 1908, BookRarity.Common),
    new BookEntry("The Road to Oz",                         "L. Frank Baum",         BookGenre.Fantasy, 1909, BookRarity.Common),
    new BookEntry("The Emerald City of Oz",                 "L. Frank Baum",         BookGenre.Fantasy, 1910, BookRarity.Common),
    new BookEntry("The Patchwork Girl of Oz",               "L. Frank Baum",         BookGenre.Fantasy, 1913, BookRarity.Common),
    new BookEntry("Tik-Tok of Oz",                          "L. Frank Baum",         BookGenre.Fantasy, 1914, BookRarity.Common),
    new BookEntry("Phantastes",                             "George MacDonald",      BookGenre.Fantasy, 1858, BookRarity.Uncommon),
    new BookEntry("Lilith",                                 "George MacDonald",      BookGenre.Fantasy, 1895, BookRarity.Rare),
    new BookEntry("The Princess and the Goblin",            "George MacDonald",      BookGenre.Fantasy, 1872, BookRarity.Common),
    new BookEntry("The Princess and Curdie",                "George MacDonald",      BookGenre.Fantasy, 1883, BookRarity.Common),
    new BookEntry("At the Back of the North Wind",          "George MacDonald",      BookGenre.Fantasy, 1871, BookRarity.Common),
    new BookEntry("The Wood Beyond the World",              "William Morris",        BookGenre.Fantasy, 1894, BookRarity.Uncommon),
    new BookEntry("The Well at the World's End",            "William Morris",        BookGenre.Fantasy, 1896, BookRarity.Uncommon),
    new BookEntry("The Water of the Wondrous Isles",        "William Morris",        BookGenre.Fantasy, 1897, BookRarity.Uncommon),
    new BookEntry("The Story of the Glittering Plain",      "William Morris",        BookGenre.Fantasy, 1891, BookRarity.Common),
    new BookEntry("The Sundering Flood",                    "William Morris",        BookGenre.Fantasy, 1897, BookRarity.Common),
    new BookEntry("A Princess of Mars",                     "Edgar Rice Burroughs", BookGenre.Fantasy, 1912, BookRarity.Common),
    new BookEntry("The Gods of Mars",                       "Edgar Rice Burroughs", BookGenre.Fantasy, 1913, BookRarity.Common),
    new BookEntry("The Warlord of Mars",                    "Edgar Rice Burroughs", BookGenre.Fantasy, 1914, BookRarity.Common),
    new BookEntry("Thuvia, Maid of Mars",                   "Edgar Rice Burroughs", BookGenre.Fantasy, 1920, BookRarity.Common),
    new BookEntry("The Chessmen of Mars",                   "Edgar Rice Burroughs", BookGenre.Fantasy, 1922, BookRarity.Common),
    new BookEntry("Tarzan of the Apes",                     "Edgar Rice Burroughs", BookGenre.Fantasy, 1914, BookRarity.Common),
    new BookEntry("The Return of Tarzan",                   "Edgar Rice Burroughs", BookGenre.Fantasy, 1915, BookRarity.Common),
    new BookEntry("Tarzan and the Jewels of Opar",          "Edgar Rice Burroughs", BookGenre.Fantasy, 1918, BookRarity.Common),
    new BookEntry("The King in Yellow",                     "Robert W. Chambers",   BookGenre.Fantasy, 1895, BookRarity.Rare),
    new BookEntry("The Maker of Moons",                     "Robert W. Chambers",   BookGenre.Fantasy, 1896, BookRarity.Uncommon),
    new BookEntry("The Great God Pan",                      "Arthur Machen",        BookGenre.Fantasy, 1894, BookRarity.Rare),
    new BookEntry("The Hill of Dreams",                     "Arthur Machen",        BookGenre.Fantasy, 1907, BookRarity.Uncommon),
    new BookEntry("The White People",                       "Arthur Machen",        BookGenre.Fantasy, 1904, BookRarity.Uncommon),
    new BookEntry("The House of Souls",                     "Arthur Machen",        BookGenre.Fantasy, 1906, BookRarity.Uncommon),
    new BookEntry("The Three Imposters",                    "Arthur Machen",        BookGenre.Fantasy, 1895, BookRarity.Common),
    new BookEntry("Vathek",                                 "William Beckford",     BookGenre.Fantasy, 1786, BookRarity.Legendary),
    new BookEntry("The Arabian Nights Entertainments",      "Anonymous",            BookGenre.Fantasy, 1706, BookRarity.Legendary),
    new BookEntry("Baron Munchausen",                       "Rudolf Raspe",         BookGenre.Fantasy, 1785, BookRarity.Uncommon),
    new BookEntry("Gulliver's Travels",                     "Jonathan Swift",       BookGenre.Fantasy, 1726, BookRarity.Rare),
    new BookEntry("The Pilgrim's Progress",                 "John Bunyan",          BookGenre.Fantasy, 1678, BookRarity.Uncommon),
    new BookEntry("Peter Pan",                              "J.M. Barrie",          BookGenre.Fantasy, 1911, BookRarity.Rare),
    new BookEntry("The Little White Bird",                  "J.M. Barrie",          BookGenre.Fantasy, 1902, BookRarity.Common),
    new BookEntry("A Midsummer Night's Dream",              "William Shakespeare",  BookGenre.Fantasy, 1600, BookRarity.Legendary),
    new BookEntry("The Tempest",                            "William Shakespeare",  BookGenre.Fantasy, 1611, BookRarity.Rare),
    new BookEntry("Orlando Furioso",                        "Ludovico Ariosto",     BookGenre.Fantasy, 1516, BookRarity.Legendary),
    new BookEntry("Jerusalem Delivered",                    "Torquato Tasso",       BookGenre.Fantasy, 1581, BookRarity.Legendary),
    new BookEntry("The Faerie Queene",                      "Edmund Spenser",       BookGenre.Fantasy, 1590, BookRarity.Legendary),
    new BookEntry("The Golden Ass",                         "Apuleius",             BookGenre.Fantasy, 158,  BookRarity.Legendary),
    new BookEntry("The Voyage of Bran",                     "Anonymous",            BookGenre.Fantasy, 700,  BookRarity.Legendary),
    new BookEntry("The Mabinogion",                         "Anonymous",            BookGenre.Fantasy, 1200, BookRarity.Legendary),
    new BookEntry("Puck of Pook's Hill",                    "Rudyard Kipling",      BookGenre.Fantasy, 1906, BookRarity.Common),
    new BookEntry("Just So Stories",                        "Rudyard Kipling",      BookGenre.Fantasy, 1902, BookRarity.Common),
    new BookEntry("The Jungle Book",                        "Rudyard Kipling",      BookGenre.Fantasy, 1894, BookRarity.Common),
    new BookEntry("The Second Jungle Book",                 "Rudyard Kipling",      BookGenre.Fantasy, 1895, BookRarity.Common),
    new BookEntry("Five Children and It",                   "E. Nesbit",            BookGenre.Fantasy, 1902, BookRarity.Common),
    new BookEntry("The Phoenix and the Carpet",             "E. Nesbit",            BookGenre.Fantasy, 1904, BookRarity.Common),
    new BookEntry("The Enchanted Castle",                   "E. Nesbit",            BookGenre.Fantasy, 1907, BookRarity.Common),
    new BookEntry("The Story of the Amulet",                "E. Nesbit",            BookGenre.Fantasy, 1906, BookRarity.Common),
    new BookEntry("The House of Arden",                     "E. Nesbit",            BookGenre.Fantasy, 1908, BookRarity.Common),
    new BookEntry("The Sword in the Stone",                 "T.H. White",           BookGenre.Fantasy, 1938, BookRarity.Uncommon),
    new BookEntry("Mistress of Mistresses",                 "E.R. Eddison",         BookGenre.Fantasy, 1935, BookRarity.Rare),
    new BookEntry("The Worm Ouroboros",                     "E.R. Eddison",         BookGenre.Fantasy, 1922, BookRarity.Rare),
    new BookEntry("A Voyage to Arcturus",                   "David Lindsay",        BookGenre.Fantasy, 1920, BookRarity.Rare),
    new BookEntry("The Night Land",                         "William Hope Hodgson", BookGenre.Fantasy, 1912, BookRarity.Rare),
    new BookEntry("The House on the Borderland",            "William Hope Hodgson", BookGenre.Fantasy, 1908, BookRarity.Uncommon),
    new BookEntry("The Boats of the Glen Carrig",           "William Hope Hodgson", BookGenre.Fantasy, 1907, BookRarity.Common),
    new BookEntry("Jurgen",                                 "James Branch Cabell",  BookGenre.Fantasy, 1919, BookRarity.Uncommon),
    new BookEntry("Figures of Earth",                       "James Branch Cabell",  BookGenre.Fantasy, 1921, BookRarity.Common),
    new BookEntry("The Cream of the Jest",                  "James Branch Cabell",  BookGenre.Fantasy, 1917, BookRarity.Common),
    new BookEntry("The Silver Stallion",                    "James Branch Cabell",  BookGenre.Fantasy, 1926, BookRarity.Common),
    new BookEntry("Something About Eve",                    "James Branch Cabell",  BookGenre.Fantasy, 1927, BookRarity.Common),
    new BookEntry("At the Earth's Core",                    "Edgar Rice Burroughs", BookGenre.Fantasy, 1914, BookRarity.Common),
    new BookEntry("Pellucidar",                             "Edgar Rice Burroughs", BookGenre.Fantasy, 1915, BookRarity.Common),
    new BookEntry("Tanar of Pellucidar",                    "Edgar Rice Burroughs", BookGenre.Fantasy, 1929, BookRarity.Common),
    new BookEntry("The Land That Time Forgot",              "Edgar Rice Burroughs", BookGenre.Fantasy, 1918, BookRarity.Common),
    new BookEntry("The People That Time Forgot",            "Edgar Rice Burroughs", BookGenre.Fantasy, 1918, BookRarity.Common),
    new BookEntry("The Lost Continent",                     "Edgar Rice Burroughs", BookGenre.Fantasy, 1916, BookRarity.Common),
    new BookEntry("The Gods of Pegana",                     "Lord Dunsany",         BookGenre.Fantasy, 1905, BookRarity.Rare),
    new BookEntry("Time and the Gods",                      "Lord Dunsany",         BookGenre.Fantasy, 1906, BookRarity.Uncommon),
    new BookEntry("The Sword of Welleran",                  "Lord Dunsany",         BookGenre.Fantasy, 1908, BookRarity.Uncommon),
    new BookEntry("A Dreamer's Tales",                      "Lord Dunsany",         BookGenre.Fantasy, 1910, BookRarity.Uncommon),
    new BookEntry("The Book of Wonder",                     "Lord Dunsany",         BookGenre.Fantasy, 1912, BookRarity.Uncommon),
    new BookEntry("Tales of Three Hemispheres",             "Lord Dunsany",         BookGenre.Fantasy, 1919, BookRarity.Common),
    new BookEntry("The King of Elfland's Daughter",         "Lord Dunsany",         BookGenre.Fantasy, 1924, BookRarity.Rare),
    new BookEntry("The Charwoman's Shadow",                 "Lord Dunsany",         BookGenre.Fantasy, 1926, BookRarity.Common),
    new BookEntry("The Blessing of Pan",                    "Lord Dunsany",         BookGenre.Fantasy, 1927, BookRarity.Common),
    new BookEntry("Lud-in-the-Mist",                       "Hope Mirrlees",        BookGenre.Fantasy, 1926, BookRarity.Legendary),
    new BookEntry("The Shining Pyramid",                    "Arthur Machen",        BookGenre.Fantasy, 1895, BookRarity.Uncommon),
    new BookEntry("Turjan of Miir",                         "Jack Vance",           BookGenre.Fantasy, 1950, BookRarity.Rare),
    new BookEntry("The Dying Earth",                        "Jack Vance",           BookGenre.Fantasy, 1950, BookRarity.Legendary),
    new BookEntry("The Devil's Dictionary",                 "Ambrose Bierce",       BookGenre.Fantasy, 1911, BookRarity.Uncommon),
    new BookEntry("Can Such Things Be",                     "Ambrose Bierce",       BookGenre.Fantasy, 1893, BookRarity.Common),
    new BookEntry("The Narrative of Arthur Gordon Pym",     "Edgar Allan Poe",      BookGenre.Fantasy, 1838, BookRarity.Uncommon),
    new BookEntry("The Smoky God",                          "Willis George Emerson",BookGenre.Fantasy, 1908, BookRarity.Common),
    new BookEntry("A Plunge into Space",                    "Robert Cromie",        BookGenre.Fantasy, 1890, BookRarity.Common),
    new BookEntry("Melmoth the Wanderer",                   "Charles Robert Maturin",BookGenre.Fantasy, 1820, BookRarity.Rare),
    new BookEntry("The Monk",                               "Matthew Gregory Lewis", BookGenre.Fantasy, 1796, BookRarity.Uncommon),
    new BookEntry("St. Leon",                               "William Godwin",       BookGenre.Fantasy, 1799, BookRarity.Common),
    new BookEntry("The Watcher by the Threshold",           "John Buchan",          BookGenre.Fantasy, 1902, BookRarity.Common),
    new BookEntry("The Moon Endureth",                      "John Buchan",          BookGenre.Fantasy, 1912, BookRarity.Common),

    // ╔══════════════════════════════════════════════════════════╗
    // ║  SCIFI — 100 творів                                     ║
    // ╚══════════════════════════════════════════════════════════╝

    new BookEntry("The Time Machine",                       "H.G. Wells",           BookGenre.SciFi, 1895, BookRarity.Rare),
    new BookEntry("The War of the Worlds",                  "H.G. Wells",           BookGenre.SciFi, 1898, BookRarity.Rare),
    new BookEntry("The Island of Doctor Moreau",            "H.G. Wells",           BookGenre.SciFi, 1896, BookRarity.Uncommon),
    new BookEntry("The Invisible Man",                      "H.G. Wells",           BookGenre.SciFi, 1897, BookRarity.Uncommon),
    new BookEntry("The First Men in the Moon",              "H.G. Wells",           BookGenre.SciFi, 1901, BookRarity.Uncommon),
    new BookEntry("The Food of the Gods",                   "H.G. Wells",           BookGenre.SciFi, 1904, BookRarity.Common),
    new BookEntry("In the Days of the Comet",               "H.G. Wells",           BookGenre.SciFi, 1906, BookRarity.Common),
    new BookEntry("The War in the Air",                     "H.G. Wells",           BookGenre.SciFi, 1908, BookRarity.Common),
    new BookEntry("The World Set Free",                     "H.G. Wells",           BookGenre.SciFi, 1914, BookRarity.Common),
    new BookEntry("Men Like Gods",                          "H.G. Wells",           BookGenre.SciFi, 1923, BookRarity.Common),
    new BookEntry("Twenty Thousand Leagues Under the Sea",  "Jules Verne",          BookGenre.SciFi, 1870, BookRarity.Rare),
    new BookEntry("Journey to the Center of the Earth",     "Jules Verne",          BookGenre.SciFi, 1864, BookRarity.Uncommon),
    new BookEntry("Around the World in Eighty Days",        "Jules Verne",          BookGenre.SciFi, 1872, BookRarity.Common),
    new BookEntry("From the Earth to the Moon",             "Jules Verne",          BookGenre.SciFi, 1865, BookRarity.Common),
    new BookEntry("The Mysterious Island",                  "Jules Verne",          BookGenre.SciFi, 1874, BookRarity.Uncommon),
    new BookEntry("Michel Strogoff",                        "Jules Verne",          BookGenre.SciFi, 1876, BookRarity.Common),
    new BookEntry("Five Weeks in a Balloon",                "Jules Verne",          BookGenre.SciFi, 1863, BookRarity.Common),
    new BookEntry("The Clipper of the Clouds",              "Jules Verne",          BookGenre.SciFi, 1886, BookRarity.Common),
    new BookEntry("Master of the World",                    "Jules Verne",          BookGenre.SciFi, 1904, BookRarity.Common),
    new BookEntry("The Purchase of the North Pole",         "Jules Verne",          BookGenre.SciFi, 1889, BookRarity.Common),
    new BookEntry("Frankenstein",                           "Mary Shelley",         BookGenre.SciFi, 1818, BookRarity.Legendary),
    new BookEntry("The Last Man",                           "Mary Shelley",         BookGenre.SciFi, 1826, BookRarity.Rare),
    new BookEntry("R.U.R.",                                 "Karel Čapek",          BookGenre.SciFi, 1920, BookRarity.Legendary),
    new BookEntry("War with the Newts",                     "Karel Čapek",          BookGenre.SciFi, 1936, BookRarity.Rare),
    new BookEntry("The Absolute at Large",                  "Karel Čapek",          BookGenre.SciFi, 1922, BookRarity.Uncommon),
    new BookEntry("Krakatit",                               "Karel Čapek",          BookGenre.SciFi, 1924, BookRarity.Uncommon),
    new BookEntry("I (Romance)", "Mykola Khvylovy", BookGenre.SciFi, 1924, BookRarity.Legendary),
    new BookEntry("Last and First Men",                     "Olaf Stapledon",       BookGenre.SciFi, 1930, BookRarity.Legendary),
    new BookEntry("Star Maker",                             "Olaf Stapledon",       BookGenre.SciFi, 1937, BookRarity.Legendary),
    new BookEntry("Odd John",                               "Olaf Stapledon",       BookGenre.SciFi, 1935, BookRarity.Rare),
    new BookEntry("Ralph 124C 41+",                         "Hugo Gernsback",       BookGenre.SciFi, 1911, BookRarity.Rare),
    new BookEntry("The Crack of Doom",                      "Robert Cromie",        BookGenre.SciFi, 1895, BookRarity.Uncommon),
    new BookEntry("Before Adam",                            "Jack London",          BookGenre.SciFi, 1906, BookRarity.Common),
    new BookEntry("The Iron Heel",                          "Jack London",          BookGenre.SciFi, 1908, BookRarity.Uncommon),
    new BookEntry("The Scarlet Plague",                     "Jack London",          BookGenre.SciFi, 1915, BookRarity.Common),
    new BookEntry("The Star Rover",                         "Jack London",          BookGenre.SciFi, 1915, BookRarity.Common),
    new BookEntry("When Worlds Collide",                    "Philip Wylie",         BookGenre.SciFi, 1933, BookRarity.Uncommon),
    new BookEntry("After Worlds Collide",                   "Philip Wylie",         BookGenre.SciFi, 1934, BookRarity.Uncommon),
    new BookEntry("The Burroughs Stories",                  "Edgar Rice Burroughs", BookGenre.SciFi, 1917, BookRarity.Common),
    new BookEntry("The Moon Maid",                          "Edgar Rice Burroughs", BookGenre.SciFi, 1926, BookRarity.Common),
    new BookEntry("The Moon Men",                           "Edgar Rice Burroughs", BookGenre.SciFi, 1925, BookRarity.Common),
    new BookEntry("The Eternal Savage",                     "Edgar Rice Burroughs", BookGenre.SciFi, 1914, BookRarity.Common),
    new BookEntry("The Mad King",                           "Edgar Rice Burroughs", BookGenre.SciFi, 1926, BookRarity.Common),
    new BookEntry("The Cave Girl",                          "Edgar Rice Burroughs", BookGenre.SciFi, 1925, BookRarity.Common),
    new BookEntry("The Mucker",                             "Edgar Rice Burroughs", BookGenre.SciFi, 1921, BookRarity.Common),
    new BookEntry("Looking Backward",                       "Edward Bellamy",       BookGenre.SciFi, 1888, BookRarity.Uncommon),
    new BookEntry("Equality",                               "Edward Bellamy",       BookGenre.SciFi, 1897, BookRarity.Common),
    new BookEntry("News from Nowhere",                      "William Morris",       BookGenre.SciFi, 1890, BookRarity.Uncommon),
    new BookEntry("Erewhon",                                "Samuel Butler",        BookGenre.SciFi, 1872, BookRarity.Uncommon),
    new BookEntry("Erewhon Revisited",                      "Samuel Butler",        BookGenre.SciFi, 1901, BookRarity.Common),
    new BookEntry("The Coming Race",                        "Edward Bulwer-Lytton", BookGenre.SciFi, 1871, BookRarity.Uncommon),
    new BookEntry("The Strange Adventures of Captain Kettle","Cutcliffe Hyne",      BookGenre.SciFi, 1898, BookRarity.Common),
    new BookEntry("Caesar's Column",                        "Ignatius Donnelly",    BookGenre.SciFi, 1890, BookRarity.Common),
    new BookEntry("The Time Traveler",                      "H.G. Wells",           BookGenre.SciFi, 1895, BookRarity.Rare),
    new BookEntry("The Purple Cloud",                       "M.P. Shiel",           BookGenre.SciFi, 1901, BookRarity.Rare),
    new BookEntry("The Lord of the Sea",                    "M.P. Shiel",           BookGenre.SciFi, 1901, BookRarity.Uncommon),
    new BookEntry("The Yellow Danger",                      "M.P. Shiel",           BookGenre.SciFi, 1898, BookRarity.Common),
    new BookEntry("A Plunge into Space",                    "Robert Cromie",        BookGenre.SciFi, 1890, BookRarity.Common),
    new BookEntry("The Brick Moon",                         "Edward Everett Hale",  BookGenre.SciFi, 1869, BookRarity.Uncommon),
    new BookEntry("In the Year 2889",                       "Jules Verne",          BookGenre.SciFi, 1889, BookRarity.Common),
    new BookEntry("The Crystal Age",                        "W.H. Hudson",          BookGenre.SciFi, 1887, BookRarity.Common),
    new BookEntry("A Strange Manuscript Found in a Copper Vessel","James De Mille", BookGenre.SciFi, 1888, BookRarity.Common),
    new BookEntry("Flatland",                               "Edwin Abbott",         BookGenre.SciFi, 1884, BookRarity.Uncommon),
    new BookEntry("Rupert of Hentzau",                      "Anthony Hope",         BookGenre.SciFi, 1898, BookRarity.Common),
    new BookEntry("The Prisoner of Zenda",                  "Anthony Hope",         BookGenre.SciFi, 1894, BookRarity.Common),
    new BookEntry("Dr. Jekyll and Mr. Hyde",                "Robert Louis Stevenson",BookGenre.SciFi, 1886, BookRarity.Rare),
    new BookEntry("The Stolen Bacillus",                    "H.G. Wells",           BookGenre.SciFi, 1895, BookRarity.Common),
    new BookEntry("The Plattner Story",                     "H.G. Wells",           BookGenre.SciFi, 1897, BookRarity.Common),
    new BookEntry("Tales of Space and Time",                "H.G. Wells",           BookGenre.SciFi, 1899, BookRarity.Common),
    new BookEntry("Twelve Stories and a Dream",             "H.G. Wells",           BookGenre.SciFi, 1903, BookRarity.Common),
    new BookEntry("The Country of the Blind",               "H.G. Wells",           BookGenre.SciFi, 1911, BookRarity.Common),
    new BookEntry("The Star",                               "H.G. Wells",           BookGenre.SciFi, 1897, BookRarity.Common),
    new BookEntry("Abyss of Wonders",                       "Otis Adelbert Kline",  BookGenre.SciFi, 1953, BookRarity.Common),
    new BookEntry("The Planet of Peril",                    "Otis Adelbert Kline",  BookGenre.SciFi, 1929, BookRarity.Common),
    new BookEntry("Darkness and Dawn",                      "George Allan England", BookGenre.SciFi, 1914, BookRarity.Common),
    new BookEntry("The Airlords of Han",                    "Philip Francis Nowlan",BookGenre.SciFi, 1929, BookRarity.Common),
    new BookEntry("Armageddon 2419 AD",                     "Philip Francis Nowlan",BookGenre.SciFi, 1928, BookRarity.Common),
    new BookEntry("The Skylark of Space",                   "Edward E. Smith",      BookGenre.SciFi, 1928, BookRarity.Uncommon),
    new BookEntry("The Mightiest Machine",                  "John W. Campbell",     BookGenre.SciFi, 1947, BookRarity.Common),
    new BookEntry("Invaders from the Infinite",             "John W. Campbell",     BookGenre.SciFi, 1932, BookRarity.Common),
    new BookEntry("Islands of Space",                       "John W. Campbell",     BookGenre.SciFi, 1931, BookRarity.Common),
    new BookEntry("The Black Star Passes",                  "John W. Campbell",     BookGenre.SciFi, 1953, BookRarity.Common),
    new BookEntry("Out of the Silent Planet",               "C.S. Lewis",           BookGenre.SciFi, 1938, BookRarity.Uncommon),
    new BookEntry("Perelandra",                             "C.S. Lewis",           BookGenre.SciFi, 1943, BookRarity.Uncommon),
    new BookEntry("That Hideous Strength",                  "C.S. Lewis",           BookGenre.SciFi, 1945, BookRarity.Uncommon),
    new BookEntry("The Power of Darkness", "Volodymyr Vynnychenko", BookGenre.SciFi, 1900, BookRarity.Uncommon),
    new BookEntry("The Beauty and the Strength", "Volodymyr Vynnychenko", BookGenre.SciFi, 1902, BookRarity.Uncommon),
    new BookEntry("The Honest Lie", "Volodymyr Vynnychenko", BookGenre.SciFi, 1907, BookRarity.Common),
    new BookEntry("Micromegas",                             "Voltaire",             BookGenre.SciFi, 1752, BookRarity.Legendary),
    new BookEntry("The Man in the Moone",                   "Francis Godwin",       BookGenre.SciFi, 1638, BookRarity.Legendary),
    new BookEntry("Somnium",                                "Johannes Kepler",      BookGenre.SciFi, 1634, BookRarity.Legendary),
    new BookEntry("A True Story",                           "Lucian of Samosata",   BookGenre.SciFi, 170,  BookRarity.Legendary),
    new BookEntry("New Atlantis",                           "Francis Bacon",        BookGenre.SciFi, 1627, BookRarity.Legendary),
    new BookEntry("The Blazing World",                      "Margaret Cavendish",   BookGenre.SciFi, 1666, BookRarity.Legendary),
    new BookEntry("Trip to the Moon",                       "Cyrano de Bergerac",   BookGenre.SciFi, 1657, BookRarity.Legendary),
    new BookEntry("The Martian",                            "Edwin Lester Arnold",  BookGenre.SciFi, 1897, BookRarity.Common),
    new BookEntry("Lieut. Gulliver Jones",                  "Edwin Lester Arnold",  BookGenre.SciFi, 1905, BookRarity.Common),
    new BookEntry("The Air Pirate",                         "Sidney Fowler Wright",BookGenre.SciFi, 1933, BookRarity.Common),
    new BookEntry("The Prophets", "Volodymyr Vynnychenko", BookGenre.SciFi, 1914, BookRarity.Common),
    new BookEntry("Deluge",                                 "Sydney Fowler Wright", BookGenre.SciFi, 1927, BookRarity.Common),

    // ╔══════════════════════════════════════════════════════════╗
    // ║  HORROR — 100 творів                                    ║
    // ╚══════════════════════════════════════════════════════════╝

    new BookEntry("Dracula",                                "Bram Stoker",          BookGenre.Horror, 1897, BookRarity.Legendary),
    new BookEntry("The Lair of the White Worm",             "Bram Stoker",          BookGenre.Horror, 1911, BookRarity.Rare),
    new BookEntry("The Jewel of Seven Stars",               "Bram Stoker",          BookGenre.Horror, 1903, BookRarity.Uncommon),
    new BookEntry("The Lady of the Shroud",                 "Bram Stoker",          BookGenre.Horror, 1909, BookRarity.Common),
    new BookEntry("The Mystery of the Sea",                 "Bram Stoker",          BookGenre.Horror, 1902, BookRarity.Common),
    new BookEntry("The Picture of Dorian Gray",             "Oscar Wilde",          BookGenre.Horror, 1890, BookRarity.Rare),
    new BookEntry("Carmilla",                               "Sheridan Le Fanu",     BookGenre.Horror, 1872, BookRarity.Rare),
    new BookEntry("Uncle Silas",                            "Sheridan Le Fanu",     BookGenre.Horror, 1864, BookRarity.Uncommon),
    new BookEntry("The House by the Churchyard",            "Sheridan Le Fanu",     BookGenre.Horror, 1863, BookRarity.Uncommon),
    new BookEntry("In a Glass Darkly",                      "Sheridan Le Fanu",     BookGenre.Horror, 1872, BookRarity.Uncommon),
    new BookEntry("The Turn of the Screw",                  "Henry James",          BookGenre.Horror, 1898, BookRarity.Uncommon),
    new BookEntry("The Call of Cthulhu",                    "H.P. Lovecraft",       BookGenre.Horror, 1926, BookRarity.Rare),
    new BookEntry("The Dunwich Horror",                     "H.P. Lovecraft",       BookGenre.Horror, 1929, BookRarity.Rare),
    new BookEntry("At the Mountains of Madness",            "H.P. Lovecraft",       BookGenre.Horror, 1936, BookRarity.Legendary),
    new BookEntry("The Shadow over Innsmouth",              "H.P. Lovecraft",       BookGenre.Horror, 1936, BookRarity.Rare),
    new BookEntry("The Shadow out of Time",                 "H.P. Lovecraft",       BookGenre.Horror, 1936, BookRarity.Rare),
    new BookEntry("The Case of Charles Dexter Ward",        "H.P. Lovecraft",       BookGenre.Horror, 1941, BookRarity.Legendary),
    new BookEntry("Herbert West: Reanimator",               "H.P. Lovecraft",       BookGenre.Horror, 1922, BookRarity.Uncommon),
    new BookEntry("The Colour out of Space",                "H.P. Lovecraft",       BookGenre.Horror, 1927, BookRarity.Uncommon),
    new BookEntry("The Whisperer in Darkness",              "H.P. Lovecraft",       BookGenre.Horror, 1931, BookRarity.Uncommon),
    new BookEntry("The Horror at Red Hook",                 "H.P. Lovecraft",       BookGenre.Horror, 1927, BookRarity.Common),
    new BookEntry("Pickman's Model",                        "H.P. Lovecraft",       BookGenre.Horror, 1927, BookRarity.Common),
    new BookEntry("The Rats in the Walls",                  "H.P. Lovecraft",       BookGenre.Horror, 1924, BookRarity.Common),
    new BookEntry("The Music of Erich Zann",                "H.P. Lovecraft",       BookGenre.Horror, 1922, BookRarity.Common),
    new BookEntry("The Lurking Fear",                       "H.P. Lovecraft",       BookGenre.Horror, 1923, BookRarity.Common),
    new BookEntry("The Yellow Wallpaper",                   "Charlotte Perkins Gilman", BookGenre.Horror, 1892, BookRarity.Common),
    new BookEntry("The Mysteries of Udolpho",               "Ann Radcliffe",        BookGenre.Horror, 1794, BookRarity.Uncommon),
    new BookEntry("The Italian",                            "Ann Radcliffe",        BookGenre.Horror, 1797, BookRarity.Uncommon),
    new BookEntry("The Romance of the Forest",              "Ann Radcliffe",        BookGenre.Horror, 1791, BookRarity.Common),
    new BookEntry("The Castle of Otranto",                  "Horace Walpole",       BookGenre.Horror, 1764, BookRarity.Legendary),
    new BookEntry("The Monk",                               "Matthew Gregory Lewis",BookGenre.Horror, 1796, BookRarity.Rare),
    new BookEntry("Vathek",                                 "William Beckford",     BookGenre.Horror, 1786, BookRarity.Rare),
    new BookEntry("Wieland",                                "Charles Brockden Brown",BookGenre.Horror, 1798, BookRarity.Uncommon),
    new BookEntry("Arthur Mervyn",                          "Charles Brockden Brown",BookGenre.Horror, 1799, BookRarity.Common),
    new BookEntry("Edgar Huntly",                           "Charles Brockden Brown",BookGenre.Horror, 1799, BookRarity.Common),
    new BookEntry("Melmoth the Wanderer",                   "Charles Robert Maturin",BookGenre.Horror, 1820, BookRarity.Rare),
    new BookEntry("The Fall of the House of Usher",         "Edgar Allan Poe",      BookGenre.Horror, 1839, BookRarity.Legendary),
    new BookEntry("The Tell-Tale Heart",                    "Edgar Allan Poe",      BookGenre.Horror, 1843, BookRarity.Rare),
    new BookEntry("The Raven",                              "Edgar Allan Poe",      BookGenre.Horror, 1845, BookRarity.Rare),
    new BookEntry("The Masque of the Red Death",            "Edgar Allan Poe",      BookGenre.Horror, 1842, BookRarity.Rare),
    new BookEntry("Berenice",                               "Edgar Allan Poe",      BookGenre.Horror, 1835, BookRarity.Uncommon),
    new BookEntry("Morella",                                "Edgar Allan Poe",      BookGenre.Horror, 1835, BookRarity.Common),
    new BookEntry("Ligeia",                                 "Edgar Allan Poe",      BookGenre.Horror, 1838, BookRarity.Uncommon),
    new BookEntry("The Pit and the Pendulum",               "Edgar Allan Poe",      BookGenre.Horror, 1842, BookRarity.Uncommon),
    new BookEntry("The Cask of Amontillado",                "Edgar Allan Poe",      BookGenre.Horror, 1846, BookRarity.Common),
    new BookEntry("The Black Cat",                          "Edgar Allan Poe",      BookGenre.Horror, 1843, BookRarity.Common),
    new BookEntry("William Wilson",                         "Edgar Allan Poe",      BookGenre.Horror, 1839, BookRarity.Common),
    new BookEntry("The Strange Case of Dr Jekyll and Mr Hyde","Robert Louis Stevenson",BookGenre.Horror,1886,BookRarity.Rare),
    new BookEntry("Thrawn Janet",                           "Robert Louis Stevenson",BookGenre.Horror, 1881, BookRarity.Common),
    new BookEntry("Markheim",                               "Robert Louis Stevenson",BookGenre.Horror, 1886, BookRarity.Common),
    new BookEntry("The Body Snatcher",                      "Robert Louis Stevenson",BookGenre.Horror, 1884, BookRarity.Common),
    new BookEntry("The Great God Pan",                      "Arthur Machen",        BookGenre.Horror, 1894, BookRarity.Rare),
    new BookEntry("The White People",                       "Arthur Machen",        BookGenre.Horror, 1904, BookRarity.Uncommon),
    new BookEntry("The Hill of Dreams",                     "Arthur Machen",        BookGenre.Horror, 1907, BookRarity.Uncommon),
    new BookEntry("The Terror",                             "Arthur Machen",        BookGenre.Horror, 1917, BookRarity.Common),
    new BookEntry("The Bowmen",                             "Arthur Machen",        BookGenre.Horror, 1914, BookRarity.Common),
    new BookEntry("The Ghost Ship",                         "Richard Middleton",    BookGenre.Horror, 1912, BookRarity.Common),
    new BookEntry("The Ghost Stories of an Antiquary",      "M.R. James",           BookGenre.Horror, 1904, BookRarity.Rare),
    new BookEntry("More Ghost Stories",                     "M.R. James",           BookGenre.Horror, 1911, BookRarity.Rare),
    new BookEntry("A Thin Ghost and Others",                "M.R. James",           BookGenre.Horror, 1919, BookRarity.Uncommon),
    new BookEntry("A Warning to the Curious",               "M.R. James",           BookGenre.Horror, 1925, BookRarity.Uncommon),
    new BookEntry("Oh Whistle and I'll Come to You",        "M.R. James",           BookGenre.Horror, 1904, BookRarity.Common),
    new BookEntry("The House of the Nightmare",             "Edward Lucas White",   BookGenre.Horror, 1917, BookRarity.Common),
    new BookEntry("The Night Wire",                         "H.F. Arnold",          BookGenre.Horror, 1926, BookRarity.Common),
    new BookEntry("The Phantom Rickshaw",                   "Rudyard Kipling",      BookGenre.Horror, 1888, BookRarity.Common),
    new BookEntry("The Mark of the Beast",                  "Rudyard Kipling",      BookGenre.Horror, 1890, BookRarity.Common),
    new BookEntry("The Man who Would be King",              "Rudyard Kipling",      BookGenre.Horror, 1888, BookRarity.Common),
    new BookEntry("Ghost Stories and Tales of Mystery",     "Sheridan Le Fanu",     BookGenre.Horror, 1851, BookRarity.Uncommon),
    new BookEntry("The Wendigo",                            "Algernon Blackwood",   BookGenre.Horror, 1910, BookRarity.Rare),
    new BookEntry("The Willows",                            "Algernon Blackwood",   BookGenre.Horror, 1907, BookRarity.Rare),
    new BookEntry("John Silence",                           "Algernon Blackwood",   BookGenre.Horror, 1908, BookRarity.Uncommon),
    new BookEntry("The Empty House",                        "Algernon Blackwood",   BookGenre.Horror, 1906, BookRarity.Common),
    new BookEntry("Incredible Adventures",                  "Algernon Blackwood",   BookGenre.Horror, 1914, BookRarity.Common),
    new BookEntry("Pan's Garden",                           "Algernon Blackwood",   BookGenre.Horror, 1912, BookRarity.Common),
    new BookEntry("The Centaur",                            "Algernon Blackwood",   BookGenre.Horror, 1911, BookRarity.Common),
    new BookEntry("Julius LeVallon",                        "Algernon Blackwood",   BookGenre.Horror, 1916, BookRarity.Common),
    new BookEntry("The Wave",                               "Algernon Blackwood",   BookGenre.Horror, 1916, BookRarity.Common),
    new BookEntry("The Promise of Air",                     "Algernon Blackwood",   BookGenre.Horror, 1918, BookRarity.Common),
    new BookEntry("The House on the Borderland",            "William Hope Hodgson", BookGenre.Horror, 1908, BookRarity.Rare),
    new BookEntry("The Night Land",                         "William Hope Hodgson", BookGenre.Horror, 1912, BookRarity.Rare),
    new BookEntry("Carnacki the Ghost-Finder",              "William Hope Hodgson", BookGenre.Horror, 1913, BookRarity.Uncommon),
    new BookEntry("The Ghost Pirates",                      "William Hope Hodgson", BookGenre.Horror, 1909, BookRarity.Common),
    new BookEntry("The Boats of the Glen Carrig",           "William Hope Hodgson", BookGenre.Horror, 1907, BookRarity.Common),
    new BookEntry("The Ring and the Book",                  "Robert Browning",      BookGenre.Horror, 1869, BookRarity.Common),
    new BookEntry("The Vampire",                            "John Polidori",        BookGenre.Horror, 1819, BookRarity.Uncommon),
    new BookEntry("The Vampyre and Other Tales",            "John Polidori",        BookGenre.Horror, 1819, BookRarity.Common),
    new BookEntry("Varney the Vampire",                     "James Malcolm Rymer",  BookGenre.Horror, 1847, BookRarity.Uncommon),
    new BookEntry("The Beetle",                             "Richard Marsh",        BookGenre.Horror, 1897, BookRarity.Uncommon),
    new BookEntry("The Parasite",                           "Arthur Conan Doyle",   BookGenre.Horror, 1894, BookRarity.Common),
    new BookEntry("The Horror of the Heights",              "Arthur Conan Doyle",   BookGenre.Horror, 1913, BookRarity.Common),
    new BookEntry("The Great Keinplatz Experiment",         "Arthur Conan Doyle",   BookGenre.Horror, 1885, BookRarity.Common),
    new BookEntry("The Captain of the Polestar",            "Arthur Conan Doyle",   BookGenre.Horror, 1883, BookRarity.Common),
    new BookEntry("The Silver Horde",                       "Rex Beach",            BookGenre.Horror, 1909, BookRarity.Common),
    new BookEntry("The God of His Fathers",                 "Jack London",          BookGenre.Horror, 1901, BookRarity.Common),
    new BookEntry("The House of Pride",                     "Jack London",          BookGenre.Horror, 1912, BookRarity.Common),
    new BookEntry("The Strength of the Strong",             "Jack London",          BookGenre.Horror, 1914, BookRarity.Common),
    new BookEntry("Ambrose Bierce: Tales",                  "Ambrose Bierce",       BookGenre.Horror, 1891, BookRarity.Uncommon),
    new BookEntry("Castles of Athlin and Dunbayne",         "Ann Radcliffe",        BookGenre.Horror, 1789, BookRarity.Common),
    new BookEntry("The Old English Baron",                  "Clara Reeve",          BookGenre.Horror, 1778, BookRarity.Common),
    new BookEntry("In the Midst of Life",                   "Ambrose Bierce",       BookGenre.Horror, 1892, BookRarity.Uncommon),

    // ╔══════════════════════════════════════════════════════════╗
    // ║  MYSTERY — 100 творів                                   ║
    // ╚══════════════════════════════════════════════════════════╝

    new BookEntry("The Adventures of Sherlock Holmes",      "Arthur Conan Doyle",   BookGenre.Mystery, 1892, BookRarity.Common),
    new BookEntry("The Memoirs of Sherlock Holmes",         "Arthur Conan Doyle",   BookGenre.Mystery, 1894, BookRarity.Common),
    new BookEntry("The Return of Sherlock Holmes",          "Arthur Conan Doyle",   BookGenre.Mystery, 1905, BookRarity.Common),
    new BookEntry("His Last Bow",                           "Arthur Conan Doyle",   BookGenre.Mystery, 1917, BookRarity.Common),
    new BookEntry("The Case-Book of Sherlock Holmes",       "Arthur Conan Doyle",   BookGenre.Mystery, 1927, BookRarity.Common),
    new BookEntry("A Study in Scarlet",                     "Arthur Conan Doyle",   BookGenre.Mystery, 1887, BookRarity.Rare),
    new BookEntry("The Sign of the Four",                   "Arthur Conan Doyle",   BookGenre.Mystery, 1890, BookRarity.Rare),
    new BookEntry("The Hound of the Baskervilles",          "Arthur Conan Doyle",   BookGenre.Mystery, 1902, BookRarity.Legendary),
    new BookEntry("The Valley of Fear",                     "Arthur Conan Doyle",   BookGenre.Mystery, 1915, BookRarity.Uncommon),
    new BookEntry("The Moonstone",                          "Wilkie Collins",       BookGenre.Mystery, 1868, BookRarity.Legendary),
    new BookEntry("The Woman in White",                     "Wilkie Collins",       BookGenre.Mystery, 1859, BookRarity.Rare),
    new BookEntry("Armadale",                               "Wilkie Collins",       BookGenre.Mystery, 1866, BookRarity.Uncommon),
    new BookEntry("Poor Miss Finch",                        "Wilkie Collins",       BookGenre.Mystery, 1872, BookRarity.Common),
    new BookEntry("The Law and the Lady",                   "Wilkie Collins",       BookGenre.Mystery, 1875, BookRarity.Common),
    new BookEntry("The Mystery of the Yellow Room",         "Gaston Leroux",        BookGenre.Mystery, 1907, BookRarity.Rare),
    new BookEntry("The Perfume of the Lady in Black",       "Gaston Leroux",        BookGenre.Mystery, 1909, BookRarity.Uncommon),
    new BookEntry("The Phantom of the Opera",               "Gaston Leroux",        BookGenre.Mystery, 1910, BookRarity.Rare),
    new BookEntry("Arsène Lupin, Gentleman Burglar",        "Maurice Leblanc",      BookGenre.Mystery, 1907, BookRarity.Common),
    new BookEntry("Arsène Lupin vs. Herlock Sholmes",       "Maurice Leblanc",      BookGenre.Mystery, 1908, BookRarity.Common),
    new BookEntry("The Hollow Needle",                      "Maurice Leblanc",      BookGenre.Mystery, 1909, BookRarity.Common),
    new BookEntry("813",                                    "Maurice Leblanc",      BookGenre.Mystery, 1910, BookRarity.Common),
    new BookEntry("The Crystal Stopper",                    "Maurice Leblanc",      BookGenre.Mystery, 1912, BookRarity.Common),
    new BookEntry("The Confessions of Arsène Lupin",        "Maurice Leblanc",      BookGenre.Mystery, 1913, BookRarity.Common),
    new BookEntry("The Double Affair",                      "Maurice Leblanc",      BookGenre.Mystery, 1920, BookRarity.Common),
    new BookEntry("The Big Bow Mystery",                    "Israel Zangwill",      BookGenre.Mystery, 1892, BookRarity.Uncommon),
    new BookEntry("The Mystery of the Blue Train",          "Agatha Christie",      BookGenre.Mystery, 1928, BookRarity.Rare),
    new BookEntry("The Murder of Roger Ackroyd",            "Agatha Christie",      BookGenre.Mystery, 1926, BookRarity.Legendary),
    new BookEntry("The Murder at the Vicarage",             "Agatha Christie",      BookGenre.Mystery, 1930, BookRarity.Uncommon),
    new BookEntry("The Seven Dials Mystery",                "Agatha Christie",      BookGenre.Mystery, 1929, BookRarity.Common),
    new BookEntry("The Red House Mystery",                  "A.A. Milne",           BookGenre.Mystery, 1922, BookRarity.Uncommon),
    new BookEntry("The Mysterious Affair at Styles",        "Agatha Christie",      BookGenre.Mystery, 1920, BookRarity.Common),
    new BookEntry("Poirot Investigates",                    "Agatha Christie",      BookGenre.Mystery, 1924, BookRarity.Common),
    new BookEntry("The Man in the Brown Suit",              "Agatha Christie",      BookGenre.Mystery, 1924, BookRarity.Common),
    new BookEntry("The Secret Adversary",                   "Agatha Christie",      BookGenre.Mystery, 1922, BookRarity.Common),
    new BookEntry("The Secret of Chimneys",                 "Agatha Christie",      BookGenre.Mystery, 1925, BookRarity.Common),
    new BookEntry("The Murder on the Links",                "Agatha Christie",      BookGenre.Mystery, 1923, BookRarity.Common),
    new BookEntry("The Murders in the Rue Morgue",          "Edgar Allan Poe",      BookGenre.Mystery, 1841, BookRarity.Legendary),
    new BookEntry("The Mystery of Marie Rogêt",            "Edgar Allan Poe",      BookGenre.Mystery, 1842, BookRarity.Rare),
    new BookEntry("The Purloined Letter",                   "Edgar Allan Poe",      BookGenre.Mystery, 1844, BookRarity.Rare),
    new BookEntry("The Gold Bug",                           "Edgar Allan Poe",      BookGenre.Mystery, 1843, BookRarity.Uncommon),
    new BookEntry("Thou Art the Man",                       "Edgar Allan Poe",      BookGenre.Mystery, 1844, BookRarity.Common),
    new BookEntry("The Leavenworth Case",                   "Anna Katharine Green", BookGenre.Mystery, 1878, BookRarity.Uncommon),
    new BookEntry("The Circular Staircase",                 "Mary Roberts Rinehart",BookGenre.Mystery, 1908, BookRarity.Common),
    new BookEntry("The Man in Lower Ten",                   "Mary Roberts Rinehart",BookGenre.Mystery, 1909, BookRarity.Common),
    new BookEntry("The Window at the White Cat",            "Mary Roberts Rinehart",BookGenre.Mystery, 1910, BookRarity.Common),
    new BookEntry("Where There's a Will",                   "Mary Roberts Rinehart",BookGenre.Mystery, 1912, BookRarity.Common),
    new BookEntry("The After House",                        "Mary Roberts Rinehart",BookGenre.Mystery, 1914, BookRarity.Common),
    new BookEntry("Trent's Last Case",                      "E.C. Bentley",         BookGenre.Mystery, 1913, BookRarity.Uncommon),
    new BookEntry("The Red House Mystery",                  "A.A. Milne",           BookGenre.Mystery, 1922, BookRarity.Common),
    new BookEntry("The Cask",                               "Freeman Wills Crofts", BookGenre.Mystery, 1920, BookRarity.Common),
    new BookEntry("Inspector French's Greatest Case",       "Freeman Wills Crofts", BookGenre.Mystery, 1924, BookRarity.Common),
    new BookEntry("The Ponson Case",                        "Freeman Wills Crofts", BookGenre.Mystery, 1921, BookRarity.Common),
    new BookEntry("The Pit-Prop Syndicate",                 "Freeman Wills Crofts", BookGenre.Mystery, 1922, BookRarity.Common),
    new BookEntry("The Hand in the Dark",                   "Arthur J. Rees",       BookGenre.Mystery, 1920, BookRarity.Common),
    new BookEntry("The Mystery of the Sycamore",            "Carolyn Wells",        BookGenre.Mystery, 1921, BookRarity.Common),
    new BookEntry("The Room with the Tassels",              "Carolyn Wells",        BookGenre.Mystery, 1918, BookRarity.Common),
    new BookEntry("The Vanishing of Betty Varian",          "Carolyn Wells",        BookGenre.Mystery, 1922, BookRarity.Common),
    new BookEntry("The Mystery of Hunting's End",           "Mignon G. Eberhart",   BookGenre.Mystery, 1930, BookRarity.Common),
    new BookEntry("While the Patient Slept",                "Mignon G. Eberhart",   BookGenre.Mystery, 1930, BookRarity.Common),
    new BookEntry("The White Cockatoo",                     "Mignon G. Eberhart",   BookGenre.Mystery, 1933, BookRarity.Common),
    new BookEntry("The Dark Garden",                        "Mignon G. Eberhart",   BookGenre.Mystery, 1933, BookRarity.Common),
    new BookEntry("Crime and the Criminal",                 "Richard Marsh",        BookGenre.Mystery, 1901, BookRarity.Common),
    new BookEntry("The Crime at Diana's Pool",              "Victor L. Whitechurch",BookGenre.Mystery, 1927, BookRarity.Common),
    new BookEntry("The Old Man in the Corner",              "Baroness Orczy",       BookGenre.Mystery, 1909, BookRarity.Uncommon),
    new BookEntry("Lady Molly of Scotland Yard",            "Baroness Orczy",       BookGenre.Mystery, 1910, BookRarity.Common),
    new BookEntry("Unravelled Knots",                       "Baroness Orczy",       BookGenre.Mystery, 1925, BookRarity.Common),
    new BookEntry("The Scarlet Pimpernel",                  "Baroness Orczy",       BookGenre.Mystery, 1905, BookRarity.Common),
    new BookEntry("Martin Hewitt, Investigator",            "Arthur Morrison",      BookGenre.Mystery, 1894, BookRarity.Common),
    new BookEntry("Chronicles of Martin Hewitt",            "Arthur Morrison",      BookGenre.Mystery, 1895, BookRarity.Common),
    new BookEntry("Adventures of Martin Hewitt",            "Arthur Morrison",      BookGenre.Mystery, 1896, BookRarity.Common),
    new BookEntry("The Dorrington Deed-Box",                "Arthur Morrison",      BookGenre.Mystery, 1897, BookRarity.Common),
    new BookEntry("The Mystery of the Clasped Hands",       "Guy Boothby",          BookGenre.Mystery, 1901, BookRarity.Common),
    new BookEntry("Dr. Nikola Returns",                     "Guy Boothby",          BookGenre.Mystery, 1896, BookRarity.Common),
    new BookEntry("A Bid for Fortune",                      "Guy Boothby",          BookGenre.Mystery, 1895, BookRarity.Common),
    new BookEntry("The Silent House",                       "Fergus Hume",          BookGenre.Mystery, 1899, BookRarity.Common),
    new BookEntry("The Mystery of a Hansom Cab",            "Fergus Hume",          BookGenre.Mystery, 1886, BookRarity.Uncommon),
    new BookEntry("Madame Midas",                           "Fergus Hume",          BookGenre.Mystery, 1888, BookRarity.Common),
    new BookEntry("Max Carrados",                           "Ernest Bramah",        BookGenre.Mystery, 1914, BookRarity.Uncommon),
    new BookEntry("The Eyes of Max Carrados",               "Ernest Bramah",        BookGenre.Mystery, 1923, BookRarity.Common),
    new BookEntry("Max Carrados Mysteries",                 "Ernest Bramah",        BookGenre.Mystery, 1927, BookRarity.Common),
    new BookEntry("Rogue Herries",                          "Hugh Walpole",         BookGenre.Mystery, 1930, BookRarity.Common),
    new BookEntry("The Rasp",                               "Philip MacDonald",     BookGenre.Mystery, 1924, BookRarity.Common),
    new BookEntry("The Link",                               "Philip MacDonald",     BookGenre.Mystery, 1930, BookRarity.Common),
    new BookEntry("The Maze",                               "Philip MacDonald",     BookGenre.Mystery, 1932, BookRarity.Common),
    new BookEntry("The Noose",                              "Philip MacDonald",     BookGenre.Mystery, 1930, BookRarity.Common),
    new BookEntry("Mystery at Geneva",                      "Rose Macaulay",        BookGenre.Mystery, 1922, BookRarity.Common),
    new BookEntry("What Really Happened",                   "Marie Belloc Lowndes", BookGenre.Mystery, 1926, BookRarity.Common),
    new BookEntry("The Lodger",                             "Marie Belloc Lowndes", BookGenre.Mystery, 1913, BookRarity.Uncommon),
    new BookEntry("Good Old Anna",                          "Marie Belloc Lowndes", BookGenre.Mystery, 1915, BookRarity.Common),
    new BookEntry("The Story of Ivy",                       "Marie Belloc Lowndes", BookGenre.Mystery, 1927, BookRarity.Common),
    new BookEntry("Suspense",                               "Joseph Conrad",        BookGenre.Mystery, 1925, BookRarity.Common),
    new BookEntry("The Secret Sharer",                      "Joseph Conrad",        BookGenre.Mystery, 1910, BookRarity.Common),
    new BookEntry("The Secret Agent",                       "Joseph Conrad",        BookGenre.Mystery, 1907, BookRarity.Uncommon),
    new BookEntry("The Man Who Was Thursday",               "G.K. Chesterton",      BookGenre.Mystery, 1908, BookRarity.Rare),
    new BookEntry("The Innocence of Father Brown",          "G.K. Chesterton",      BookGenre.Mystery, 1911, BookRarity.Rare),
    new BookEntry("The Wisdom of Father Brown",             "G.K. Chesterton",      BookGenre.Mystery, 1914, BookRarity.Uncommon),
    new BookEntry("The Incredulity of Father Brown",        "G.K. Chesterton",      BookGenre.Mystery, 1926, BookRarity.Uncommon),
    new BookEntry("The Secret of Father Brown",             "G.K. Chesterton",      BookGenre.Mystery, 1927, BookRarity.Uncommon),
    new BookEntry("The Scandal of Father Brown",            "G.K. Chesterton",      BookGenre.Mystery, 1935, BookRarity.Common),
    new BookEntry("The Donnington Affair",                  "M.R. James",           BookGenre.Mystery, 1914, BookRarity.Common),

    // ╔══════════════════════════════════════════════════════════╗
    // ║  BIOGRAPHY — 100 творів                                 ║
    // ╚══════════════════════════════════════════════════════════╝

    new BookEntry("The Autobiography of Benjamin Franklin", "Benjamin Franklin",    BookGenre.Biography, 1791, BookRarity.Rare),
    new BookEntry("Poor Richard's Almanack",                "Benjamin Franklin",    BookGenre.Biography, 1732, BookRarity.Common),
    new BookEntry("Up from Slavery",                        "Booker T. Washington", BookGenre.Biography, 1901, BookRarity.Common),
    new BookEntry("My Larger Education",                    "Booker T. Washington", BookGenre.Biography, 1911, BookRarity.Common),
    new BookEntry("Personal Memoirs of Ulysses S. Grant",   "Ulysses S. Grant",     BookGenre.Biography, 1885, BookRarity.Legendary),
    new BookEntry("The Story of My Life",                   "Helen Keller",         BookGenre.Biography, 1903, BookRarity.Rare),
    new BookEntry("Midstream",                              "Helen Keller",         BookGenre.Biography, 1929, BookRarity.Common),
    new BookEntry("The World I Live In",                    "Helen Keller",         BookGenre.Biography, 1908, BookRarity.Common),
    new BookEntry("Narrative of the Life of Frederick Douglass","Frederick Douglass",BookGenre.Biography, 1845, BookRarity.Common),
    new BookEntry("My Bondage and My Freedom",              "Frederick Douglass",   BookGenre.Biography, 1855, BookRarity.Uncommon),
    new BookEntry("Life and Times of Frederick Douglass",   "Frederick Douglass",   BookGenre.Biography, 1881, BookRarity.Uncommon),
    new BookEntry("The Life of Samuel Johnson",             "James Boswell",        BookGenre.Biography, 1791, BookRarity.Legendary),
    new BookEntry("Confessions",                            "Jean-Jacques Rousseau",BookGenre.Biography, 1782, BookRarity.Rare),
    new BookEntry("Reveries of the Solitary Walker",        "Jean-Jacques Rousseau",BookGenre.Biography, 1782, BookRarity.Uncommon),
    new BookEntry("Lives of the Noble Greeks and Romans",   "Plutarch",             BookGenre.Biography, 100,  BookRarity.Legendary),
    new BookEntry("Parallel Lives",                         "Plutarch",             BookGenre.Biography, 100,  BookRarity.Legendary),
    new BookEntry("The Confessions of St. Augustine",       "Saint Augustine",      BookGenre.Biography, 400,  BookRarity.Legendary),
    new BookEntry("The Life of St. Teresa of Ávila",        "Teresa of Ávila",      BookGenre.Biography, 1565, BookRarity.Legendary),
    new BookEntry("The Life of Charlemagne",                "Einhard",              BookGenre.Biography, 830,  BookRarity.Legendary),
    new BookEntry("The History of the Kings of Britain",    "Geoffrey of Monmouth", BookGenre.Biography, 1136, BookRarity.Legendary),
    new BookEntry("Memoirs of Napoleon Bonaparte",          "Louis Antoine Bourrienne", BookGenre.Biography, 1831, BookRarity.Rare),
    new BookEntry("Life of Napoleon Bonaparte",             "Walter Scott",         BookGenre.Biography, 1827, BookRarity.Uncommon),
    new BookEntry("Napoleon and His Marshals",              "Joel Tyler Headley",   BookGenre.Biography, 1846, BookRarity.Common),
    new BookEntry("Memoirs of Casanova",                    "Giacomo Casanova",     BookGenre.Biography, 1798, BookRarity.Uncommon),
    new BookEntry("The Memoirs of Sherlock Holmes",         "Arthur Conan Doyle",   BookGenre.Biography, 1894, BookRarity.Common),
    new BookEntry("Adventures of a Younger Son",            "Edward John Trelawny", BookGenre.Biography, 1831, BookRarity.Common),
    new BookEntry("The Recollections of Geoffrey Hamlyn",   "Henry Kingsley",       BookGenre.Biography, 1859, BookRarity.Common),
    new BookEntry("The Autobiography of Mark Twain",        "Mark Twain",           BookGenre.Biography, 1924, BookRarity.Rare),
    new BookEntry("Life on the Mississippi",                "Mark Twain",           BookGenre.Biography, 1883, BookRarity.Common),
    new BookEntry("Roughing It",                            "Mark Twain",           BookGenre.Biography, 1872, BookRarity.Common),
    new BookEntry("The Innocents Abroad",                   "Mark Twain",           BookGenre.Biography, 1869, BookRarity.Common),
    new BookEntry("A Tramp Abroad",                         "Mark Twain",           BookGenre.Biography, 1880, BookRarity.Common),
    new BookEntry("Following the Equator",                  "Mark Twain",           BookGenre.Biography, 1897, BookRarity.Common),
    new BookEntry("My Autobiography",                       "Andrew Carnegie",      BookGenre.Biography, 1920, BookRarity.Uncommon),
    new BookEntry("The Autobiography of Andrew Carnegie",   "Andrew Carnegie",      BookGenre.Biography, 1920, BookRarity.Uncommon),
    new BookEntry("The Memoirs of Madame de Rémusat",      "Claire de Rémusat",    BookGenre.Biography, 1879, BookRarity.Uncommon),
    new BookEntry("Memoirs of the Comtesse de Boigne",      "Adèle de Boigne",      BookGenre.Biography, 1907, BookRarity.Common),
    new BookEntry("The Private Memoirs of Napoleon",        "Constant Wairy",       BookGenre.Biography, 1895, BookRarity.Common),
    new BookEntry("Recollections of a Long Life",           "Joseph Gurney Bevan",  BookGenre.Biography, 1902, BookRarity.Common),
    new BookEntry("The Education of Henry Adams",           "Henry Adams",          BookGenre.Biography, 1907, BookRarity.Rare),
    new BookEntry("Mont Saint Michel and Chartres",         "Henry Adams",          BookGenre.Biography, 1904, BookRarity.Uncommon),
    new BookEntry("My Reminiscences",                       "Rabindranath Tagore",  BookGenre.Biography, 1917, BookRarity.Uncommon),
    new BookEntry("My Experiments with Truth",              "Mahatma Gandhi",       BookGenre.Biography, 1927, BookRarity.Rare),
    new BookEntry("The Story of My Experiments with Truth", "Mahatma Gandhi",       BookGenre.Biography, 1929, BookRarity.Rare),
    new BookEntry("Stray Birds",                            "Rabindranath Tagore",  BookGenre.Biography, 1916, BookRarity.Common),
    new BookEntry("My Life and Work",                       "Henry Ford",           BookGenre.Biography, 1922, BookRarity.Common),
    new BookEntry("Today and Tomorrow",                     "Henry Ford",           BookGenre.Biography, 1926, BookRarity.Common),
    new BookEntry("My Life",                                "Isadora Duncan",       BookGenre.Biography, 1927, BookRarity.Uncommon),
    new BookEntry("My Own Story",                           "Emmeline Pankhurst",   BookGenre.Biography, 1914, BookRarity.Uncommon),
    new BookEntry("The Woman Warrior",                      "Mary Wollstonecraft",  BookGenre.Biography, 1792, BookRarity.Uncommon),
    new BookEntry("A Vindication of the Rights of Woman",   "Mary Wollstonecraft",  BookGenre.Biography, 1792, BookRarity.Rare),
    new BookEntry("Life of Charlotte Brontë",               "Elizabeth Gaskell",    BookGenre.Biography, 1857, BookRarity.Uncommon),
    new BookEntry("Memoirs of My Dead Life",                "George Moore",         BookGenre.Biography, 1906, BookRarity.Common),
    new BookEntry("Hail and Farewell",                      "George Moore",         BookGenre.Biography, 1911, BookRarity.Common),
    new BookEntry("The Summing Up",                         "W. Somerset Maugham",  BookGenre.Biography, 1938, BookRarity.Common),
    new BookEntry("On the Wing of Occasions",               "Joel Chandler Harris", BookGenre.Biography, 1900, BookRarity.Common),
    new BookEntry("Adventures of Eovaai",                   "Eliza Haywood",        BookGenre.Biography, 1736, BookRarity.Common),
    new BookEntry("Account of the Life of Shakespeare",     "Nicholas Rowe",        BookGenre.Biography, 1709, BookRarity.Uncommon),
    new BookEntry("Lives of the English Poets",             "Samuel Johnson",       BookGenre.Biography, 1779, BookRarity.Uncommon),
    new BookEntry("Life of Dryden",                         "Samuel Johnson",       BookGenre.Biography, 1779, BookRarity.Common),
    new BookEntry("Life of Pope",                           "Samuel Johnson",       BookGenre.Biography, 1779, BookRarity.Common),
    new BookEntry("The Life of Nelson",                     "Robert Southey",       BookGenre.Biography, 1813, BookRarity.Uncommon),
    new BookEntry("The Life of Wellington",                 "Herbert Maxwell",      BookGenre.Biography, 1900, BookRarity.Common),
    new BookEntry("The Personal Memoirs of P.H. Sheridan",  "Philip H. Sheridan",   BookGenre.Biography, 1888, BookRarity.Common),
    new BookEntry("Memoirs of William T. Sherman",          "William T. Sherman",   BookGenre.Biography, 1875, BookRarity.Common),
    new BookEntry("Jefferson Davis: A Memoir",              "Varina Davis",         BookGenre.Biography, 1890, BookRarity.Common),
    new BookEntry("The Autobiography of a Pocket Handkerchief","James Fenimore Cooper",BookGenre.Biography, 1843, BookRarity.Common),
    new BookEntry("Mii zhyttievyi shliakh", "Ivan Franko", BookGenre.Biography, 1911, BookRarity.Common),
    new BookEntry("The Way I Grew", "Olha Kobylianska", BookGenre.Biography, 1900, BookRarity.Common),
    new BookEntry("Valse Melancolique", "Olha Kobylianska", BookGenre.Biography, 1898, BookRarity.Uncommon),
    new BookEntry("Nature", "Olha Kobylianska", BookGenre.Biography, 1895, BookRarity.Common),
    new BookEntry("Liudyna", "Olha Kobylianska", BookGenre.Biography, 1891, BookRarity.Common),
    new BookEntry("The Institute Girl", "Marko Vovchok", BookGenre.Biography, 1861, BookRarity.Uncommon),
    new BookEntry("Memoirs of a Superfluous Man",           "Albert Jay Nock",      BookGenre.Biography, 1943, BookRarity.Common),
    new BookEntry("Twice Told Tales",                       "Nathaniel Hawthorne",  BookGenre.Biography, 1837, BookRarity.Common),
    new BookEntry("Sketches by Boz",                        "Charles Dickens",      BookGenre.Biography, 1836, BookRarity.Common),
    new BookEntry("American Notes",                         "Charles Dickens",      BookGenre.Biography, 1842, BookRarity.Common),
    new BookEntry("Pictures from Italy",                    "Charles Dickens",      BookGenre.Biography, 1846, BookRarity.Common),
    new BookEntry("The Letters of Charles Dickens",         "Charles Dickens",      BookGenre.Biography, 1880, BookRarity.Common),
    new BookEntry("Essays in London",                       "Henry James",          BookGenre.Biography, 1893, BookRarity.Common),
    new BookEntry("Portraits of Places",                    "Henry James",          BookGenre.Biography, 1883, BookRarity.Common),
    new BookEntry("A Little Tour in France",                "Henry James",          BookGenre.Biography, 1884, BookRarity.Common),
    new BookEntry("English Hours",                          "Henry James",          BookGenre.Biography, 1905, BookRarity.Common),
    new BookEntry("Memories and Portraits",                 "Robert Louis Stevenson",BookGenre.Biography, 1887, BookRarity.Common),
    new BookEntry("Essays of Travel",                       "Robert Louis Stevenson",BookGenre.Biography, 1905, BookRarity.Common),
    new BookEntry("In the South Seas",                      "Robert Louis Stevenson",BookGenre.Biography, 1896, BookRarity.Common),
    new BookEntry("Travels with a Donkey in the Cévennes",  "Robert Louis Stevenson",BookGenre.Biography, 1879, BookRarity.Common),
    new BookEntry("An Inland Voyage",                       "Robert Louis Stevenson",BookGenre.Biography, 1878, BookRarity.Common),
    new BookEntry("Seven Pillars of Wisdom",                "T.E. Lawrence",        BookGenre.Biography, 1926, BookRarity.Legendary),
    new BookEntry("Revolt in the Desert",                   "T.E. Lawrence",        BookGenre.Biography, 1927, BookRarity.Rare),
    new BookEntry("Goodbye to All That",                    "Robert Graves",        BookGenre.Biography, 1929, BookRarity.Uncommon),
    new BookEntry("Storm of Steel",                         "Ernst Jünger",         BookGenre.Biography, 1920, BookRarity.Uncommon),
    new BookEntry("Good-Bye to All That",                   "Robert Graves",        BookGenre.Biography, 1929, BookRarity.Common),
    new BookEntry("Undertones of War",                      "Edmund Blunden",       BookGenre.Biography, 1928, BookRarity.Common),
    new BookEntry("A Farewell to Arms",                     "Ernest Hemingway",     BookGenre.Biography, 1929, BookRarity.Uncommon),
    new BookEntry("The Sun Also Rises",                     "Ernest Hemingway",     BookGenre.Biography, 1926, BookRarity.Common),
    new BookEntry("A Moveable Feast",                       "Ernest Hemingway",     BookGenre.Biography, 1964, BookRarity.Common),
    new BookEntry("Memories of my Life",                    "Francis Galton",       BookGenre.Biography, 1908, BookRarity.Common),
    new BookEntry("My Antonia",                             "Willa Cather",         BookGenre.Biography, 1918, BookRarity.Common),
    new BookEntry("The Letters of Horace Walpole",          "Horace Walpole",       BookGenre.Biography, 1840, BookRarity.Common),

    // ╔══════════════════════════════════════════════════════════╗
    // ║  ACADEMIC — 100 творів                                  ║
    // ╚══════════════════════════════════════════════════════════╝

    new BookEntry("On the Origin of Species",               "Charles Darwin",       BookGenre.Academic, 1859, BookRarity.Legendary),
    new BookEntry("The Descent of Man",                     "Charles Darwin",       BookGenre.Academic, 1871, BookRarity.Legendary),
    new BookEntry("The Expression of Emotions in Man and Animals","Charles Darwin", BookGenre.Academic, 1872, BookRarity.Rare),
    new BookEntry("The Voyage of the Beagle",               "Charles Darwin",       BookGenre.Academic, 1839, BookRarity.Rare),
    new BookEntry("The Prince",                             "Niccolò Machiavelli",  BookGenre.Academic, 1532, BookRarity.Legendary),
    new BookEntry("Discourses on Livy",                     "Niccolò Machiavelli",  BookGenre.Academic, 1531, BookRarity.Rare),
    new BookEntry("The Art of War",                         "Sun Tzu",              BookGenre.Academic, -500, BookRarity.Legendary),
    new BookEntry("Meditations",                            "Marcus Aurelius",      BookGenre.Academic, 180,  BookRarity.Legendary),
    new BookEntry("Leviathan",                              "Thomas Hobbes",        BookGenre.Academic, 1651, BookRarity.Legendary),
    new BookEntry("The Wealth of Nations",                  "Adam Smith",           BookGenre.Academic, 1776, BookRarity.Legendary),
    new BookEntry("The Theory of Moral Sentiments",         "Adam Smith",           BookGenre.Academic, 1759, BookRarity.Rare),
    new BookEntry("The Social Contract",                    "Jean-Jacques Rousseau",BookGenre.Academic, 1762, BookRarity.Rare),
    new BookEntry("Emile, or On Education",                 "Jean-Jacques Rousseau",BookGenre.Academic, 1762, BookRarity.Uncommon),
    new BookEntry("Critique of Pure Reason",                "Immanuel Kant",        BookGenre.Academic, 1781, BookRarity.Legendary),
    new BookEntry("Critique of Practical Reason",           "Immanuel Kant",        BookGenre.Academic, 1788, BookRarity.Rare),
    new BookEntry("Critique of Judgement",                  "Immanuel Kant",        BookGenre.Academic, 1790, BookRarity.Rare),
    new BookEntry("Groundwork of the Metaphysics of Morals","Immanuel Kant",        BookGenre.Academic, 1785, BookRarity.Uncommon),
    new BookEntry("Utopia",                                 "Thomas More",          BookGenre.Academic, 1516, BookRarity.Legendary),
    new BookEntry("The Republic",                           "Plato",                BookGenre.Academic, -375, BookRarity.Legendary),
    new BookEntry("The Symposium",                          "Plato",                BookGenre.Academic, -385, BookRarity.Rare),
    new BookEntry("Phaedo",                                 "Plato",                BookGenre.Academic, -360, BookRarity.Rare),
    new BookEntry("The Apology",                            "Plato",                BookGenre.Academic, -399, BookRarity.Rare),
    new BookEntry("Meno",                                   "Plato",                BookGenre.Academic, -380, BookRarity.Uncommon),
    new BookEntry("Timaeus",                                "Plato",                BookGenre.Academic, -360, BookRarity.Uncommon),
    new BookEntry("Nicomachean Ethics",                     "Aristotle",            BookGenre.Academic, -350, BookRarity.Legendary),
    new BookEntry("Politics",                               "Aristotle",            BookGenre.Academic, -330, BookRarity.Legendary),
    new BookEntry("Poetics",                                "Aristotle",            BookGenre.Academic, -335, BookRarity.Rare),
    new BookEntry("Metaphysics",                            "Aristotle",            BookGenre.Academic, -350, BookRarity.Legendary),
    new BookEntry("On the Soul",                            "Aristotle",            BookGenre.Academic, -350, BookRarity.Rare),
    new BookEntry("Rhetoric",                               "Aristotle",            BookGenre.Academic, -330, BookRarity.Uncommon),
    new BookEntry("The Communist Manifesto",                "Karl Marx",            BookGenre.Academic, 1848, BookRarity.Uncommon),
    new BookEntry("Das Kapital Vol. 1",                     "Karl Marx",            BookGenre.Academic, 1867, BookRarity.Legendary),
    new BookEntry("Contribution to the Critique of Political Economy","Karl Marx",  BookGenre.Academic, 1859, BookRarity.Uncommon),
    new BookEntry("Common Sense",                           "Thomas Paine",         BookGenre.Academic, 1776, BookRarity.Uncommon),
    new BookEntry("The Rights of Man",                      "Thomas Paine",         BookGenre.Academic, 1791, BookRarity.Uncommon),
    new BookEntry("The Age of Reason",                      "Thomas Paine",         BookGenre.Academic, 1794, BookRarity.Common),
    new BookEntry("On Liberty",                             "John Stuart Mill",     BookGenre.Academic, 1859, BookRarity.Rare),
    new BookEntry("Utilitarianism",                         "John Stuart Mill",     BookGenre.Academic, 1863, BookRarity.Uncommon),
    new BookEntry("Principles of Political Economy",        "John Stuart Mill",     BookGenre.Academic, 1848, BookRarity.Uncommon),
    new BookEntry("The Subjection of Women",                "John Stuart Mill",     BookGenre.Academic, 1869, BookRarity.Uncommon),
    new BookEntry("Two Treatises of Government",            "John Locke",           BookGenre.Academic, 1689, BookRarity.Rare),
    new BookEntry("An Essay Concerning Human Understanding","John Locke",           BookGenre.Academic, 1689, BookRarity.Rare),
    new BookEntry("A Letter Concerning Toleration",         "John Locke",           BookGenre.Academic, 1689, BookRarity.Uncommon),
    new BookEntry("Ethics",                                 "Baruch Spinoza",       BookGenre.Academic, 1677, BookRarity.Legendary),
    new BookEntry("Theological-Political Treatise",         "Baruch Spinoza",       BookGenre.Academic, 1670, BookRarity.Rare),
    new BookEntry("Discourse on the Method",                "René Descartes",       BookGenre.Academic, 1637, BookRarity.Rare),
    new BookEntry("Meditations on First Philosophy",        "René Descartes",       BookGenre.Academic, 1641, BookRarity.Rare),
    new BookEntry("The New Organon",                        "Francis Bacon",        BookGenre.Academic, 1620, BookRarity.Rare),
    new BookEntry("The Advancement of Learning",            "Francis Bacon",        BookGenre.Academic, 1605, BookRarity.Uncommon),
    new BookEntry("Novum Organum",                          "Francis Bacon",        BookGenre.Academic, 1620, BookRarity.Uncommon),
    new BookEntry("The Federalist Papers",                  "Hamilton, Madison, Jay",BookGenre.Academic, 1788, BookRarity.Legendary),
    new BookEntry("Democracy in America",                   "Alexis de Tocqueville",BookGenre.Academic, 1835, BookRarity.Legendary),
    new BookEntry("The Old Regime and the Revolution",      "Alexis de Tocqueville",BookGenre.Academic, 1856, BookRarity.Uncommon),
    new BookEntry("The Protestant Ethic and Capitalism",    "Max Weber",            BookGenre.Academic, 1905, BookRarity.Rare),
    new BookEntry("On the Nature of Things",                "Lucretius",            BookGenre.Academic, -60,  BookRarity.Legendary),
    new BookEntry("The Histories",                          "Herodotus",            BookGenre.Academic, -440, BookRarity.Legendary),
    new BookEntry("History of the Peloponnesian War",       "Thucydides",           BookGenre.Academic, -431, BookRarity.Legendary),
    new BookEntry("Lives of the Twelve Caesars",            "Suetonius",            BookGenre.Academic, 121,  BookRarity.Rare),
    new BookEntry("The Annals",                             "Tacitus",              BookGenre.Academic, 117,  BookRarity.Rare),
    new BookEntry("Germania",                               "Tacitus",              BookGenre.Academic, 98,   BookRarity.Uncommon),
    new BookEntry("The History of the Decline and Fall of the Roman Empire","Edward Gibbon",BookGenre.Academic, 1776, BookRarity.Legendary),
    new BookEntry("An Essay on the Principle of Population","Thomas Malthus",       BookGenre.Academic, 1798, BookRarity.Rare),
    new BookEntry("The Principles of Geology",              "Charles Lyell",        BookGenre.Academic, 1830, BookRarity.Uncommon),
    new BookEntry("Ukrainian Political Thought", "Mykhailo Drahomanov", BookGenre.Academic, 1880, BookRarity.Uncommon),
    new BookEntry("Historical Poland and Great Ruthenia", "Mykhailo Drahomanov", BookGenre.Academic, 1869, BookRarity.Uncommon),
    new BookEntry("Vybrani tvory", "Mykhailo Drahomanov", BookGenre.Academic, 1892, BookRarity.Common),
    new BookEntry("The Will to Power",                      "Friedrich Nietzsche",  BookGenre.Academic, 1901, BookRarity.Rare),
    new BookEntry("Thus Spoke Zarathustra",                 "Friedrich Nietzsche",  BookGenre.Academic, 1883, BookRarity.Legendary),
    new BookEntry("Beyond Good and Evil",                   "Friedrich Nietzsche",  BookGenre.Academic, 1886, BookRarity.Rare),
    new BookEntry("On the Genealogy of Morality",           "Friedrich Nietzsche",  BookGenre.Academic, 1887, BookRarity.Rare),
    new BookEntry("The Birth of Tragedy",                   "Friedrich Nietzsche",  BookGenre.Academic, 1872, BookRarity.Uncommon),
    new BookEntry("Ecce Homo",                              "Friedrich Nietzsche",  BookGenre.Academic, 1908, BookRarity.Uncommon),
    new BookEntry("Human, All Too Human",                   "Friedrich Nietzsche",  BookGenre.Academic, 1878, BookRarity.Common),
    new BookEntry("The Gay Science",                        "Friedrich Nietzsche",  BookGenre.Academic, 1882, BookRarity.Common),
    new BookEntry("Twilight of the Idols",                  "Friedrich Nietzsche",  BookGenre.Academic, 1889, BookRarity.Common),
    new BookEntry("The Science of Logic",                   "Georg Wilhelm Friedrich Hegel", BookGenre.Academic, 1816, BookRarity.Legendary),
    new BookEntry("Phenomenology of Spirit",                "Georg Wilhelm Friedrich Hegel", BookGenre.Academic, 1807, BookRarity.Legendary),
    new BookEntry("Philosophy of Right",                    "Georg Wilhelm Friedrich Hegel", BookGenre.Academic, 1820, BookRarity.Rare),
    new BookEntry("The World as Will and Representation",   "Arthur Schopenhauer",  BookGenre.Academic, 1818, BookRarity.Rare),
    new BookEntry("Essays and Aphorisms",                   "Arthur Schopenhauer",  BookGenre.Academic, 1851, BookRarity.Uncommon),
    new BookEntry("The Basis of Morality",                  "Arthur Schopenhauer",  BookGenre.Academic, 1840, BookRarity.Common),
    new BookEntry("Pragmatism",                             "William James",        BookGenre.Academic, 1907, BookRarity.Uncommon),
    new BookEntry("The Varieties of Religious Experience",  "William James",        BookGenre.Academic, 1902, BookRarity.Rare),
    new BookEntry("Principles of Psychology",               "William James",        BookGenre.Academic, 1890, BookRarity.Rare),
    new BookEntry("The Will to Believe",                    "William James",        BookGenre.Academic, 1897, BookRarity.Uncommon),
    new BookEntry("Talks to Teachers",                      "William James",        BookGenre.Academic, 1899, BookRarity.Common),
    new BookEntry("Democracy and Education",                "John Dewey",           BookGenre.Academic, 1916, BookRarity.Uncommon),
    new BookEntry("Reconstruction in Philosophy",           "John Dewey",           BookGenre.Academic, 1920, BookRarity.Common),
    new BookEntry("Human Nature and Conduct",               "John Dewey",           BookGenre.Academic, 1922, BookRarity.Common),
    new BookEntry("Experience and Nature",                  "John Dewey",           BookGenre.Academic, 1925, BookRarity.Common),
    new BookEntry("Creative Intelligence",                  "John Dewey",           BookGenre.Academic, 1917, BookRarity.Common),
    new BookEntry("The Golden Bough",                       "James George Frazer",  BookGenre.Academic, 1890, BookRarity.Legendary),
    new BookEntry("Totem and Taboo",                        "Sigmund Freud",        BookGenre.Academic, 1913, BookRarity.Rare),
    new BookEntry("The Interpretation of Dreams",           "Sigmund Freud",        BookGenre.Academic, 1899, BookRarity.Legendary),
    new BookEntry("Civilization and Its Discontents",       "Sigmund Freud",        BookGenre.Academic, 1930, BookRarity.Rare),
    new BookEntry("Three Essays on the Theory of Sexuality","Sigmund Freud",        BookGenre.Academic, 1905, BookRarity.Uncommon),
    new BookEntry("The Ego and the Id",                     "Sigmund Freud",        BookGenre.Academic, 1923, BookRarity.Uncommon),
    new BookEntry("Introduction to Psychoanalysis",         "Sigmund Freud",        BookGenre.Academic, 1917, BookRarity.Common),
    new BookEntry("Beyond the Pleasure Principle",          "Sigmund Freud",        BookGenre.Academic, 1920, BookRarity.Common),
    new BookEntry("Introductory Lectures on Psychoanalysis","Sigmund Freud",        BookGenre.Academic, 1917, BookRarity.Common),
    };

    // ─────────────────────────────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────────────────────────────

    private void OnEnable()  => LoadPrefs();
    private void OnDisable() => SavePrefs();

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawHeader();
        EditorGUILayout.Space(4);

        DrawSettings();
        EditorGUILayout.Space(4);

        DrawPrefabSlots();
        EditorGUILayout.Space(4);

        DrawStats();
        EditorGUILayout.Space(8);

        DrawGenerateButton();

        EditorGUILayout.EndScrollView();
    }

    // ── Секція: заголовок ────────────────────────────────────────────────
    private void DrawHeader()
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField("📚 Book Batch Generator", style, GUILayout.Height(24));
        EditorGUILayout.LabelField("Генератор BookTemplate SO для Bookstore", EditorStyles.centeredGreyMiniLabel);
    }

    // ── Секція: налаштування ─────────────────────────────────────────────
    private void DrawSettings()
    {
        EditorGUILayout.LabelField("⚙ Налаштування", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        _database = (BookDatabase)EditorGUILayout.ObjectField(
            "BookDatabase", _database, typeof(BookDatabase), false);

        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
        if (GUILayout.Button("…", GUILayout.Width(28)))
        {
            string picked = EditorUtility.OpenFolderPanel(
                "Обрати папку", Application.dataPath, "Generated");
            if (!string.IsNullOrEmpty(picked))
                _outputPath = "Assets" + picked.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        _addToDatabase  = EditorGUILayout.Toggle("Додати до BookDatabase", _addToDatabase);
        _clearExisting  = EditorGUILayout.Toggle("Видалити старі SO перед генерацією", _clearExisting);

        EditorGUI.indentLevel--;
    }

    // ── Секція: 16 слотів префабів ───────────────────────────────────────
    private void DrawPrefabSlots()
    {
        _showPrefabs = EditorGUILayout.Foldout(_showPrefabs, "🎨 16 Color Prefabs (Book001)", true);
        if (!_showPrefabs) return;

        EditorGUI.indentLevel++;

        int assigned = _colorPrefabs.Count(p => p != null);

        // Кольорова підказка: скільки призначено
        var infoStyle = assigned == 16
            ? EditorStyles.helpBox
            : EditorStyles.helpBox;
        Color prevColor = GUI.contentColor;
        GUI.contentColor = assigned == 16 ? Color.green : assigned > 0 ? Color.yellow : Color.red;
        EditorGUILayout.LabelField($"Призначено: {assigned}/16 префабів", EditorStyles.miniLabel);
        GUI.contentColor = prevColor;

        for (int i = 0; i < 16; i++)
        {
            EditorGUILayout.BeginHorizontal();

            // Кольоровий квадратик (індикатор)
            var rect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
            rect.y += 2;
            Color slotColor = _colorPrefabs[i] != null ? Color.green : new Color(0.4f, 0.4f, 0.4f);
            EditorGUI.DrawRect(rect, slotColor);

            _colorPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                $"[{i:D2}] Color_{i:D2}",
                _colorPrefabs[i],
                typeof(GameObject), false);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        // Кнопки швидкого заповнення
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍 Auto-Find у проєкті"))
            AutoFindPrefabs();
        if (GUILayout.Button("✖ Очистити всі"))
        {
            for (int i = 0; i < 16; i++) _colorPrefabs[i] = null;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
    }

    // ── Секція: статистика ───────────────────────────────────────────────
    private void DrawStats()
    {
        _showStats = EditorGUILayout.Foldout(_showStats, "📊 Статистика списку", true);
        if (!_showStats) return;

        EditorGUI.indentLevel++;

        // Підрахунок по жанрах
        var counts = new Dictionary<BookGenre, int>();
        foreach (var entry in BookData)
        {
            if (!counts.ContainsKey(entry.genre)) counts[entry.genre] = 0;
            counts[entry.genre]++;
        }

        foreach (var kv in counts.OrderBy(x => x.Key.ToString()))
            EditorGUILayout.LabelField($"{kv.Key}", $"{kv.Value} книг", EditorStyles.miniLabel);

        EditorGUILayout.LabelField("РАЗОМ", $"{BookData.Length} книг",
            new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold });

        // Попередження
        int validPrefabs = _colorPrefabs.Count(p => p != null);
        if (validPrefabs == 0)
            EditorGUILayout.HelpBox("Призначте хоча б один prefab!", MessageType.Warning);
        else if (validPrefabs < 16)
            EditorGUILayout.HelpBox($"Призначено {validPrefabs}/16 префабів. Решта будуть рандомно з наявних.", MessageType.Info);

        EditorGUI.indentLevel--;
    }

    // ── Кнопка генерації ─────────────────────────────────────────────────
    private void DrawGenerateButton()
    {
        bool canGenerate = _colorPrefabs.Any(p => p != null);

        EditorGUI.BeginDisabledGroup(!canGenerate);

        var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fixedHeight = 38 };
        if (GUILayout.Button("🚀  Генерувати всі SO", btnStyle))
            Generate();

        EditorGUI.EndDisabledGroup();

        if (!canGenerate)
            EditorGUILayout.HelpBox("Призначте хоча б один prefab щоб розблокувати генерацію.", MessageType.Warning);
    }

    // ─────────────────────────────────────────────────────────────────────
    // АВТО-ПОШУК ПРЕФАБІВ
    // ─────────────────────────────────────────────────────────────────────

    private void AutoFindPrefabs()
    {
        // Шукаємо префаби з назвами Book_Color_00..15 або Book_00..15
        string[] patterns = { "Book_Color_", "BookColor", "Book_0", "Book_1" };
        var guids = AssetDatabase.FindAssets("t:Prefab");

        int found = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            // Шукаємо "_00" .. "_15" або просто цифру в кінці
            for (int i = 0; i < 16; i++)
            {
                if (_colorPrefabs[i] != null) continue;

                bool match =
                    name.EndsWith($"_{i:D2}") ||
                    name.EndsWith($"_{i}")    ||
                    name.ToLower().Contains($"color{i:D2}") ||
                    name.ToLower().Contains($"color_{i:D2}");

                if (match)
                {
                    _colorPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    found++;
                    break;
                }
            }
        }

        if (found > 0)
            Debug.Log($"[BookGenerator] Auto-find: знайдено {found} префабів.");
        else
            EditorUtility.DisplayDialog("Auto-Find",
                "Префаби не знайдені автоматично.\nПереконайся що вони мають назви вигляду:\nBook_Color_00, Book_Color_01 ...",
                "OK");
    }

    // ─────────────────────────────────────────────────────────────────────
    // ГЕНЕРАЦІЯ
    // ─────────────────────────────────────────────────────────────────────

    private void Generate()
    {
        // Список валідних префабів
        var validPrefabs = new List<(int colorIndex, GameObject go)>();
        for (int i = 0; i < 16; i++)
            if (_colorPrefabs[i] != null)
                validPrefabs.Add((i, _colorPrefabs[i]));

        if (validPrefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("Помилка", "Призначте хоча б один prefab!", "OK");
            return;
        }

        // Очищення старих SO
        if (_clearExisting && Directory.Exists(_outputPath))
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Підтвердження",
                $"Видалити всі .asset файли з\n{_outputPath}?",
                "Так, видалити", "Скасувати");

            if (!confirmed) return;

            var oldGuids = AssetDatabase.FindAssets("t:BookTemplate", new[] { _outputPath });
            foreach (var g in oldGuids)
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));

            AssetDatabase.Refresh();
        }

        // Групуємо книги по жанрах
        var byGenre = BookData
            .GroupBy(e => e.genre)
            .ToDictionary(g => g.Key, g => g.ToList());

        int total   = 0;
        int skipped = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (var kv in byGenre)
            {
                BookGenre       genre   = kv.Key;
                List<BookEntry> entries = kv.Value;

                // Папка для жанру: Assets/Data/Books/Generated/Fantasy/
                string genreFolder = Path.Combine(_outputPath, genre.ToString())
                                         .Replace('\\', '/');

                if (!Directory.Exists(genreFolder))
                    Directory.CreateDirectory(genreFolder);

                foreach (var entry in entries)
                {
                    // Рандомний префаб зі списку наявних
                    var slot = validPrefabs[Random.Range(0, validPrefabs.Count)];

                    var so = ScriptableObject.CreateInstance<BookTemplate>();
                    so.title        = entry.title;
                    so.author       = entry.author;
                    so.genre        = entry.genre;
                    so.rarity       = entry.rarity;
                    so.writingYear  = Mathf.Max(entry.year, 1); // від'ємні роки (античність) → 1
                    so.volumeNumber = 1;
                    so.bookSize     = PrefabToBookSize(slot.go);

                    so.containerPrefab = slot.go;
                    so.colorIndex      = slot.colorIndex;

                    // Ціни за rarity
                    (so.buyPrice, so.sellPrice) = CalcPrices(entry.rarity);

                    // bookID через BookSmartID
                    so.bookID = BookSmartID.Generate(so);

                    // Безпечне ім'я файлу
                    string assetName = SanitizeFileName(entry.title);
                    string assetPath = $"{genreFolder}/BT_{assetName}.asset";
                    assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                    AssetDatabase.CreateAsset(so, assetPath);
                    total++;
                }

                Debug.Log($"[BookGenerator] {genre}: {entries.Count} SO → {genreFolder}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BookGenerator] Помилка генерації: {ex}");
            EditorUtility.DisplayDialog("Помилка", ex.Message, "OK");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Оновлюємо BookDatabase
        if (_addToDatabase && _database != null)
            RefreshDatabase();

        string summary = $"✅ Згенеровано {total} BookTemplate SO\n" +
                         $"📁 Папки: {byGenre.Count} жанри\n" +
                         $"🎨 Префабів: {validPrefabs.Count}/16";

        Debug.Log($"[BookGenerator] {summary.Replace("\n", " | ")}");
        EditorUtility.DisplayDialog("Готово!", summary, "OK");
    }

    // ─────────────────────────────────────────────────────────────────────
    // ОНОВЛЕННЯ БАЗИ ДАНИХ
    // ─────────────────────────────────────────────────────────────────────

    private void RefreshDatabase()
    {
        if (_database == null) return;

        _database.allBooks.Clear();

        var guids = AssetDatabase.FindAssets("t:BookTemplate", new[] { _outputPath });
        foreach (var guid in guids)
        {
            var bt = AssetDatabase.LoadAssetAtPath<BookTemplate>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (bt != null) _database.allBooks.Add(bt);
        }

        EditorUtility.SetDirty(_database);
        AssetDatabase.SaveAssets();

        Debug.Log($"[BookGenerator] BookDatabase оновлено: {_database.allBooks.Count} книг.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    /// Визначає BookSize з localScale префабу
    private static BookSize PrefabToBookSize(GameObject prefab)
    {
        if (prefab == null) return BookSize.Medium;
        float height = prefab.transform.localScale.y;
        if (height < 0.85f) return BookSize.Small;
        if (height > 1.15f) return BookSize.Large;
        return BookSize.Medium;
    }

    /// Ціни за rarity з невеликим рандомом
    private static (float buy, float sell) CalcPrices(BookRarity rarity)
    {
        float sell = rarity switch
        {
            BookRarity.Common    => Random.Range(8f,   20f),
            BookRarity.Uncommon  => Random.Range(18f,  45f),
            BookRarity.Rare      => Random.Range(40f,  90f),
            BookRarity.Epic      => Random.Range(80f,  150f),
            BookRarity.Legendary => Random.Range(120f, 280f),
            _ => 10f
        };
        return (Mathf.Round(sell * 0.55f), Mathf.Round(sell));
    }

    /// Прибирає символи які не можна використовувати в назві файлу Unity
    private static string SanitizeFileName(string title)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars()) { ' ', '\'', '"', ',', '.', ':', ';', '!', '?', '(', ')' };
        var sb = new System.Text.StringBuilder();
        foreach (char c in title)
            sb.Append(invalid.Contains(c) ? '_' : c);

        // Прибираємо подвійні підкреслення
        string result = sb.ToString();
        while (result.Contains("__")) result = result.Replace("__", "_");
        return result.Trim('_');
    }

    // ─────────────────────────────────────────────────────────────────────
    // EDITOR PREFS — зберігаємо стан між перезапусками Unity
    // ─────────────────────────────────────────────────────────────────────

    private void SavePrefs()
    {
        EditorPrefs.SetString(PrefKey + "outputPath",     _outputPath);
        EditorPrefs.SetBool  (PrefKey + "clearExisting",  _clearExisting);
        EditorPrefs.SetBool  (PrefKey + "addToDatabase",  _addToDatabase);

        for (int i = 0; i < 16; i++)
        {
            string path = _colorPrefabs[i] != null
                ? AssetDatabase.GetAssetPath(_colorPrefabs[i])
                : "";
            EditorPrefs.SetString(PrefKey + $"prefab{i}", path);
        }
    }

    private void LoadPrefs()
    {
        _outputPath    = EditorPrefs.GetString(PrefKey + "outputPath",    _outputPath);
        _clearExisting = EditorPrefs.GetBool  (PrefKey + "clearExisting", _clearExisting);
        _addToDatabase = EditorPrefs.GetBool  (PrefKey + "addToDatabase", _addToDatabase);

        for (int i = 0; i < 16; i++)
        {
            string path = EditorPrefs.GetString(PrefKey + $"prefab{i}", "");
            _colorPrefabs[i] = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
#endif