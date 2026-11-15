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

            // ---------------------------------------------------------------- a photo of... something else (what's in the picture)
            // themselves: they recognise their own face (two each, alternating)
            L("dexter_self_1", CastId.Dexter, "Hey, that's me! Do I look trustworthy? I feel like I look trustworthy.");
            L("dexter_self_2", CastId.Dexter, "Ooh, frame that one. Signed copies, fifty cents.");
            L("sherman_self_1", CastId.Sherman, "Me. Adequately lit. The glasses are doing most of the work.");
            L("sherman_self_2", CastId.Sherman, "A portrait of the scientist. I'll file it under S. For Sherman.");
            L("serena_self_1", CastId.Serena, "Okay, obsessed. Send me that. Like, right now.");
            L("serena_self_2", CastId.Serena, "Wait, is my hat crooked? No. It's iconic. Keep it.");
            L("lucy_self_1", CastId.Lucy, "Me. Looking competent. As usual.");
            L("lucy_self_2", CastId.Lucy, "Photographing the detective? Bold. I'm not a suspect, rookie.");
            L("tony_self_1", CastId.Tony, "Heh. Turbo, looking fresh. No cap.");
            L("tony_self_2", CastId.Tony, "Why've you got pictures of ME? ...Delete that.");
            // somebody else
            L("dexter_about_sherman", CastId.Dexter, "That's Sherman. He reads books for FUN. In the snow!");
            L("dexter_about_serena", CastId.Dexter, "Serena! She bought a cocoa once. Said it clashed with her hat.");
            L("dexter_about_lucy", CastId.Dexter, "Lucy. She charged me five cents to tell me I'm a suspect.");
            L("dexter_about_tony", CastId.Dexter, "Tony. Never pays for his cocoa. Not once.");
            L("sherman_about_dexter", CastId.Sherman, "Dexter. Ninety percent cocoa, ten percent nerves.");
            L("sherman_about_serena", CastId.Sherman, "Serena. Her hat has its own gravitational field.");
            L("sherman_about_lucy", CastId.Sherman, "Lucy. Self-appointed detective. No formal credentials.");
            L("sherman_about_tony", CastId.Sherman, "Tony. Average sledding speed: reckless.");
            L("serena_about_dexter", CastId.Serena, "Cocoa boy. His scarf literally doesn't match anything.");
            L("serena_about_sherman", CastId.Serena, "Bunny boy. Is he eating? He's always eating.");
            L("serena_about_lucy", CastId.Serena, "Miss Detective. That notebook is SO last season.");
            L("serena_about_tony", CastId.Serena, "Tony? Those goggles are, like, a whole crime.");
            L("lucy_about_dexter", CastId.Lucy, "Dexter. Jumpier than a snowman in July.");
            L("lucy_about_sherman", CastId.Lucy, "Sherman. Knows everything except where his snacks come from.");
            L("lucy_about_serena", CastId.Lucy, "Serena. She leaves glitter on everything she touches. Useful.");
            L("lucy_about_tony", CastId.Lucy, "Tony. Fast, loud, and never where you left him.");
            L("tony_about_dexter", CastId.Tony, "Cocoa kid. He owes me a refund.");
            L("tony_about_sherman", CastId.Tony, "Little nerd. Probably calculating something.");
            L("tony_about_serena", CastId.Tony, "Miss Sparkle. Don't touch her hat, bro. Trust me.");
            L("tony_about_lucy", CastId.Tony, "Detective girl. She's been staring at me all day. Weird.");
            // Frosty (before he's evidence of anything)
            L("dexter_frosty", CastId.Dexter, "Poor Frosty... I can't look. I need a cocoa.");
            L("sherman_frosty", CastId.Sherman, "The victim. Eighty-five percent water. One hundred percent tragic.");
            L("serena_frosty", CastId.Serena, "Ugh, he's a mess. And not in a cute way.");
            L("lucy_frosty", CastId.Lucy, "Yes, rookie, that's the victim. I need EVIDENCE.");
            L("tony_frosty", CastId.Tony, "Rough day for the big guy. Not my problem.");
            // the sled standing in the snow (nobody gives away whose it is)
            L("dexter_sled", CastId.Dexter, "A sled! Sledding makes me thirsty. Cocoa?");
            L("sherman_sled", CastId.Sherman, "A sled, parked vertically. An odd choice.");
            L("serena_sled", CastId.Serena, "Red's not my color, so... not mine.");
            L("lucy_sled", CastId.Lucy, "Somebody parked a sled at my crime scene. Noted.");
            L("tony_sled", CastId.Tony, "It's a sled, bro. Why are you asking me?");
            // the sand bucket and spade
            L("dexter_toys", CastId.Dexter, "A sand bucket? In the snow? Bold.");
            L("sherman_toys", CastId.Sherman, "Beach equipment in sub-zero conditions. Fascinating.");
            L("serena_toys", CastId.Serena, "A plastic bucket? That's not giving winter.");
            L("lucy_toys", CastId.Lucy, "A bucket and a spade. Not evidence. Next.");
            L("tony_toys", CastId.Tony, "Who brings beach toys to a snow day?");
            // the snowy redwoods (the Stanford tree)
            L("dexter_tree", CastId.Dexter, "Nice tree! Trees don't buy cocoa, though.");
            L("sherman_tree", CastId.Sherman, "A coast redwood. They can live two thousand years. Show-offs.");
            L("serena_tree", CastId.Serena, "Cute tree. Very... outdoorsy.");
            L("lucy_tree", CastId.Lucy, "A tree. Trees have alibis, rookie.");
            L("tony_tree", CastId.Tony, "I could totally jump that. With a sled. Probably.");
            // shrubs, rocks, logs
            L("dexter_nature", CastId.Dexter, "Pretty! Nature doesn't buy cocoa, though.");
            L("sherman_nature", CastId.Sherman, "Flora, under snow. I left my field guide at home.");
            L("serena_nature", CastId.Serena, "A bush. With snow on it. Iconic, I guess?");
            L("lucy_nature", CastId.Lucy, "Scenery. Scenery has alibis, rookie.");
            L("tony_nature", CastId.Tony, "Bro's doing nature photography now.");
            // nothing much at all (a wall, the air, the ceiling)
            L("dexter_nothing", CastId.Dexter, "Um... what am I looking at? Want a cocoa while you figure it out?");
            L("sherman_nothing", CastId.Sherman, "I detect no subject. Fascinating composition.");
            L("serena_nothing", CastId.Serena, "Is this... a mood? Bold choice.");
            L("lucy_nothing", CastId.Lucy, "That's a picture of nothing. Impressive, even for you.");
            L("tony_nothing", CastId.Tony, "Bro took a picture of the air.");
            // a blur (Tony dodging the lens in the finale)
            L("dexter_blur", CastId.Dexter, "It's all blurry. Was that Tony? He's so fast.");
            L("sherman_blur", CastId.Sherman, "Motion blur. Estimated subject velocity: Tony.");
            L("serena_blur", CastId.Serena, "Blurry. Ugh, hold the camera STILL.");
            L("lucy_blur", CastId.Lucy, "A blur. He won't sit still for you, rookie.");

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
            L("lucy_snap", CastId.Lucy, "Well? Don't just stand there. Take a picture, rookie. It'll last longer.");
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
