using System.Collections.Generic;
using System.Text;

namespace FrostAndFound
{
    public class Line
    {
        public string Id;
        public CastId Speaker;
        public string Text;
        public Line(string id, CastId speaker, string text) { Id = id; Speaker = speaker; Text = text; }
    }

    /// <summary>
    /// Every voiced line with its subtitle. Recorded audio drops in by id: put "dexter_excuse.wav" (or .mp3/.ogg)
    /// in Assets/FrostAndFound/Resources/Voice/ and it plays instead of the placeholder babble.
    /// Menu: Frost & Found ▸ Export Voice Script writes this table as a CSV for the recording session / ElevenLabs.
    /// </summary>
    public static class Lines
    {
        static readonly Dictionary<string, Line> s_Lines = new Dictionary<string, Line>();
        public static IEnumerable<Line> All => s_Lines.Values;

        static void L(string id, CastId who, string text) => s_Lines[id] = new Line(id, who, text);

        public static Line Get(string id) => s_Lines.TryGetValue(id, out var l) ? l : null;

        static Lines()
        {
            // ---------------------------------------------------------------- intro (0:00–0:10)
            L("lucy_intro", CastId.Lucy, "Great. Another detective. Don't touch anything. I touched everything already. All I've got is that big brown stain. Smells... sweet. That'll be five cents.");

            // ---------------------------------------------------------------- the clue chain (doc §3)
            L("dexter_excuse", CastId.Dexter, "I spilled my cocoa because I freaked out when I saw him like that! Oh god, my cocoa... And his nose was gone too! Actually, I saw Sherman eating a carrot earlier. Just saying.");
            L("dexter_point", CastId.Dexter, "That's him, he's the one with the glasses.");
            L("sherman_excuse", CastId.Sherman, "I needed that. Do you expect me to function in a caloric deficit? Besides, that carrot was a nutritional disappointment. I had to wipe GLITTER off it. Given Serena's glitter addiction, I'd say there's a 99.9% probability she's responsible.");
            L("sherman_point", CastId.Sherman, "That's her, the girl with the pink bag.");
            L("serena_excuse", CastId.Serena, "Um, EXCUSE me? I was giving him a makeover because, unlike SOME people, I actually have taste. There was this disgusting red scratch on his butt, though. Literally ruined the aesthetic.");
            L("lucy_excuse", CastId.Lucy, "Red paint! Obviously! If you think about it, there's only ONE kid on this street with a red sled. Honestly, I'm doing all the detective work here.");
            L("lucy_point", CastId.Lucy, "That's him – the guy with the cap.");
            L("tony_excuse", CastId.Tony, "Yeah, I got a red sled, Sherlock Homeless. You got proof it was me? Didn't think so. Catch you later.");

            // ---------------------------------------------------------------- finale (doc §6)
            L("tony_defense", CastId.Tony, "Circumstantial! No picture of ME, no case!");
            L("lucy_finale_hint", CastId.Lucy, "Well? He's not going to sit still for you.");
            L("tony_taunt_1", CastId.Tony, "Can't catch the Turbo!");
            L("tony_taunt_2", CastId.Tony, "Too slow, no cap!");
            L("tony_taunt_3", CastId.Tony, "Turbo's untouchable!");
            L("tony_dodge_1", CastId.Tony, "Nope!");
            L("tony_dodge_2", CastId.Tony, "No pics!");
            L("tony_dodge_3", CastId.Tony, "Not my good side!");
            L("tony_bonk", CastId.Tony, "Ow-wa-wa-wa...");
            L("tony_trip", CastId.Tony, "Whoa— my sled!");
            L("tony_up", CastId.Tony, "Ha! Still got it. No cap.");
            L("tony_caught", CastId.Tony, "...It WAS a sick jump though.");
            L("lucy_case_closed", CastId.Lucy, "Case closed. You're welcome.");

            // ---------------------------------------------------------------- photo poses (when they're in frame)
            L("dexter_pose", CastId.Dexter, "Say cocoa! Twenty-five cents a cup!");
            L("sherman_pose", CastId.Sherman, "Peace and proper nutrition.");
            L("serena_pose", CastId.Serena, "Ugh, fine. Get my good angle.");
            L("lucy_pose", CastId.Lucy, "No autographs.");
            L("tony_pose", CastId.Tony, "Get my good side.");

            // ---------------------------------------------------------------- snowball hits (just for fun)
            L("dexter_hit", CastId.Dexter, "Hey! My marshmallow! ...That one's fifty cents.");
            L("sherman_hit", CastId.Sherman, "My books! Alphabetical order... RUINED.");
            L("serena_hit", CastId.Serena, "My HAT!");
            L("lucy_hit", CastId.Lucy, "Assaulting a detective. Writing that down.");
            L("tony_hit_early", CastId.Tony, "No cap, that tickled.");

            // ---------------------------------------------------------------- right kid, old evidence again
            L("dexter_again", CastId.Dexter, "We've been over this. Want a cocoa while you think?");
            L("sherman_again", CastId.Sherman, "Repeating the question won't change the data.");
            L("serena_again", CastId.Serena, "I literally already told you. Keep up.");
            L("lucy_again", CastId.Lucy, "Yes, yes, red paint. Tony. Keep up, rookie.");
            L("tony_again", CastId.Tony, "Still just a sled, Sherlock.");

            // ---------------------------------------------------------------- a photo of nothing
            L("dexter_snow", CastId.Dexter, "That's a picture of snow. Want some cocoa to go with it?");
            L("sherman_snow", CastId.Sherman, "That is a photograph of precipitation.");
            L("serena_snow", CastId.Serena, "Is this... art? It's giving nothing.");
            L("lucy_snow", CastId.Lucy, "A picture of snow. Groundbreaking.");
            L("tony_snow", CastId.Tony, "Bro took a picture of the floor.");
            L("tony_blur", CastId.Tony, "Blurry! Can't prove nothing!");

            // ---------------------------------------------------------------- wrong kid: a joke line + a nudge
            // cocoa stain
            L("sherman_wrong_stain", CastId.Sherman, "Brown liquid. Not my department. Statistically, that's a cocoa guy.");
            L("serena_wrong_stain", CastId.Serena, "Ew, brown? I would NEVER. Ask the cocoa boy.");
            L("lucy_wrong_stain", CastId.Lucy, "Obviously cocoa. Obviously not me. Follow the drips, rookie.");
            L("tony_wrong_stain", CastId.Tony, "That's not even a stunt. Go bug the cocoa kid.");
            // carrot bits
            L("dexter_wrong_carrot", CastId.Dexter, "That carrot? Not mine. I only sell cocoa. Ask the kid with the glasses.");
            L("serena_wrong_carrot", CastId.Serena, "Like, I don't eat vegetables? Ask bunny boy.");
            L("lucy_wrong_carrot", CastId.Lucy, "Bite marks. Small ones. Think, rookie. Who's always snacking?");
            L("tony_wrong_carrot", CastId.Tony, "I don't eat carrots, I eat RECORDS. Ask the little nerd.");
            // purple glitter
            L("dexter_wrong_glitter", CastId.Dexter, "That's glitter. I'm a cocoa guy.");
            L("sherman_wrong_glitter", CastId.Sherman, "Purple glitter. Ninety-nine point nine percent Serena.");
            L("lucy_wrong_glitter", CastId.Lucy, "Glitter? Please. Only one person on this street sparkles that hard.");
            L("tony_wrong_glitter", CastId.Tony, "Glitter's not my vibe. Ask Miss Sparkle over there.");
            // red scrape
            L("dexter_wrong_scrape", CastId.Dexter, "Red? Lucy's the detective. Ask her. Cocoa's on me — twenty-five cents.");
            L("sherman_wrong_scrape", CastId.Sherman, "Paint analysis is outside my expertise. Lucy has a magnifying glass.");
            L("serena_wrong_scrape", CastId.Serena, "I literally already told you about the scratch. Go ask Miss Detective.");
            L("tony_wrong_scrape", CastId.Tony, "A scratch? On a snowman? Wild. Probably nothing. Ask the detective girl.");
            // dented sled
            L("dexter_wrong_sled", CastId.Dexter, "That's Tony's sled. Everybody knows Tony's sled. Cocoa for the chase?");
            L("sherman_wrong_sled", CastId.Sherman, "That dent is consistent with a high-velocity impact. Tony's sled.");
            L("serena_wrong_sled", CastId.Serena, "That ugly sled? Tony's. Obviously.");
            L("lucy_wrong_sled", CastId.Lucy, "Why are you showing ME? Show HIM.");

            // ---------------------------------------------------------------- Lucy's safety-net nudges (vague, not answers)
            L("lucy_nudge_guilty", CastId.Lucy, "Hmph. Some people look awfully guilty.");
            L("lucy_nudge_worst", CastId.Lucy, "You're the worst detective I've ever seen. And I've seen ME on a bad day.");
            L("lucy_nudge_sparkle", CastId.Lucy, "Hmph. Some things around here are awfully... sparkly.");
            L("lucy_nudge_close", CastId.Lucy, "Closer, rookie. Evidence doesn't come to you.");
            L("lucy_nudge_show", CastId.Lucy, "A photo's no good in your pocket. Show it to someone.");
            L("lucy_nudge_crouch", CastId.Lucy, "Some clues are down low. Detectives crouch.");
            L("lucy_tamper", CastId.Lucy, "Don't contaminate the crime scene!");

            // ---------------------------------------------------------------- idle barks (ambient personality)
            L("dexter_bark", CastId.Dexter, "Cocoa! Twenty-five cents! Cheaper than therapy!");
            L("sherman_bark", CastId.Sherman, "Snowmen are 85% water. The other 15% is hope.");
            L("serena_bark", CastId.Serena, "Lady Frostington would never look this tragic.");
            L("tony_bark", CastId.Tony, "Turbo's the name, speed's the game. No cap.");
        }

        public static string ExportCsv()
        {
            var sb = new StringBuilder("id,speaker,line\n");
            foreach (var l in s_Lines.Values)
                sb.Append(l.Id).Append(',').Append(l.Speaker).Append(",\"").Append(l.Text.Replace("\"", "\"\"")).Append("\"\n");
            return sb.ToString();
        }
    }
}
