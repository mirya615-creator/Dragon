using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// Wire DTOs intentionally use snake_case field names to match the authoritative
// OpenAPI document. They stay separate from UI/domain models.

[Serializable]
internal sealed class GoAuthResponse
{
    public string player_id;
    public string access_token;
    public string refresh_token;
    public string token_type;
    public int expires_in;
    public bool is_new_player;
}

[Serializable]
internal sealed class GoGoogleLoginRequest
{
    public string id_token;
    public DeviceInfoDto device_info;
}

[Serializable]
internal sealed class GoRefreshRequest
{
    public string refresh_token;
}

[Serializable]
internal sealed class GoEnergySnapshot
{
    public int current;
    public int max;
    public string next_recover_at;
    public int recover_interval_sec;
}

[Serializable]
internal sealed class GoRestoreStatus
{
    public bool enabled;
    public int daily_used;
    public int daily_limit;
}

[Serializable]
internal sealed class GoEnergyResponse
{
    public int current;
    public int max;
    public string next_recover_at;
    public int recover_interval_sec;
    public GoRestoreStatus ad_restore;
    public GoRestoreStatus share_restore;
}

[Serializable]
internal sealed class GoResources
{
    public long gold;
    public long rune_fragment;
}

[Serializable]
internal sealed class GoBootstrapResponse
{
    public GoBootstrapPlayer player;
    public GoSaveSnapshot save;
    public GoEnergySnapshot energy;
    public GoResources resources;
    public GoRankProfileResponse rank;
    public string config_version;
    public string content_version;
    public string server_time;
}

[Serializable]
internal sealed class GoBootstrapPlayer
{
    public string player_id;
    public string display_name;
    public string created_at;
    public string iana_timezone;
}

[Serializable]
internal sealed class GoRunStartRequest
{
    public string stage_id;
    public string client_version;
    public string content_version;
    public string requested_config_version;
    public string device_session_id;
}

[Serializable]
internal sealed class GoRunStartResponse
{
    public string run_id;
    public string run_seed;
    public string run_nonce;
    public string rng_version;
    public string event_version;
    public string config_version;
    public string content_version;
    public string started_at;
    public string expires_at;
    public string stage_snapshot_digest;
    public string ticket_signature;
    public string recruit_random;
    public string wave_rng;
    public List<GoItemLoadoutEntry> item_loadout_snapshot = new List<GoItemLoadoutEntry>();
}

[Serializable]
internal sealed class GoRunChainEvent
{
    // OpenAPI 0.15.0 is authoritative for the public HTTP contract.
    public long sequence;
    public string event_type;
    public string payload_digest;
    public string previous_hash;
    public string current_hash;
}

[Serializable]
internal sealed class GoRunSnapshotRequest : IUnaryRawJsonRequest
{
    public string snapshot_type;
    public int? wave_number;
    public int? current_health;
    public long event_sequence;
    public string payload;
    public string previous_hash;
    public string current_hash;

    public string ToJson()
    {
        return "{\"snapshot_type\":\"" + snapshot_type +
               "\",\"wave_number\":" + NullableInteger(wave_number) +
               ",\"current_health\":" + NullableInteger(current_health) +
               ",\"event_sequence\":" + event_sequence.ToString(CultureInfo.InvariantCulture) +
               ",\"payload\":\"" + payload +
               "\",\"previous_hash\":\"" + previous_hash +
               "\",\"current_hash\":\"" + current_hash + "\"}";
    }

    private static string NullableInteger(int? value) => value.HasValue
        ? value.Value.ToString(CultureInfo.InvariantCulture)
        : "null";
}

[Serializable]
internal sealed class GoRunSnapshotResponse
{
    public long snapshot_id;
    public string run_id;
    public long event_sequence;
    public int payload_size;
    public string payload_digest;
    public string payload_ref;
    public string current_hash;
    public string created_at;
    public bool replayed;
}

[Serializable]
internal sealed class GoRunFinishRequest
{
    public string ticket_signature;
    public string finished_at;
    public string result;
    public int reached_wave;
    public int remaining_health;
    public long final_event_sequence;
    public string final_event_hash;
    public string critical_random_sequence;
    public List<GoRunChainEvent> event_summary = new List<GoRunChainEvent>();
}

[Serializable]
internal sealed class GoRunRuneDrop
{
    public string rune_id;
    public long rune_instance_id;
    public bool full;
    public int fragments;
}

[Serializable]
internal sealed class GoRunSettlement
{
    public long base_gold;
    public bool ad_multiplied;
    public long total_gold;
    public long rune_fragments;
    public List<GoRunRuneDrop> rune_drops = new List<GoRunRuneDrop>();
}

[Serializable]
internal sealed class GoRunFinishResponse
{
    public string run_id;
    public string result;
    public string settlement_status;
    public int reached_wave;
    public int remaining_health;
    public GoRunSettlement settlement;
    public GoRunMerchantEvent merchant_event;
    public GoRunRankSettlement rank_settlement;
    public string rejection_code;
}

[Serializable]
internal sealed class GoRunRankSettlement
{
    public bool applied;
    public bool replayed;
    public int star_delta;
    public GoRankSnapshot before;
    public GoRankSnapshot after;
    public bool promoted;
    public bool demoted;
    public long version;
}

[Serializable]
internal sealed class GoRankSnapshot
{
    public string rank_id;
    public int segment;
    public long stars;
    public long total_rank_stars;
    public long version;
    public string updated_at;
}

[Serializable]
internal sealed class GoRunMerchantEvent
{
    public string event_id;
    public List<GoMerchantOffer> offers = new List<GoMerchantOffer>();
    public bool lottery_available;
}

[Serializable]
internal sealed class GoLeaderboardResponse
{
    public string board_type;
    public string period_key;
    public string start_at;
    public string end_at;
    public int page;
    public int page_size;
    public int total;
    public List<GoLeaderboardEntry> entries = new List<GoLeaderboardEntry>();
}

[Serializable]
internal sealed class GoLeaderboardEntry
{
    public string player_id;
    public string avatar_id;
    public string rank_id;
    public int segment;
    public long stars;
    public long score;
    public long rank_position;
    public string tie_breaker_at;
}

[Serializable]
internal sealed class GoRankProfileResponse
{
    public string rank_id;
    public int segment;
    public long stars;
    public long total_rank_stars;
    public long version;
    public string updated_at;
}

[Serializable]
internal sealed class GoRuneListResponse
{
    public List<GoRuneCatalogEntry> runes = new List<GoRuneCatalogEntry>();
    public List<GoPlayerRuneEntry> player_runes = new List<GoPlayerRuneEntry>();
    public long fragment_balance;
}

[Serializable]
internal sealed class GoRuneCatalogEntry
{
    public string rune_id;
    public string quality;
    public int fragment_cost;
    public string content_version;
}

[Serializable]
internal sealed class GoPlayerRuneEntry
{
    public long rune_instance_id;
    public string rune_id;
    public string source_id;
    public string created_at;
}

[Serializable]
internal sealed class GoEquipRuneRequest
{
    public long rune_instance_id;
}

[Serializable]
internal sealed class GoEquipRuneResponse
{
    public string hero_id;
    public long rune_instance_id;
}

[Serializable]
internal sealed class GoCraftRuneResponse
{
    public long rune_instance_id;
    public string rune_id;
    public int fragments_spent;
    public long balance_after;
}

[Serializable]
internal sealed class GoMerchantResponse
{
    public string event_id;
    public string day_key;
    public List<GoMerchantOffer> offers = new List<GoMerchantOffer>();
    public bool lottery_available;
}

[Serializable]
internal sealed class GoMerchantOffer
{
    public string item_id;
    public string delivery;
    public int price_gold;
    public string rarity;
    public string content_version;
    public string digest;
}

[Serializable]
internal sealed class GoMerchantClaimRequest
{
    public string item_id;
}

[Serializable]
internal sealed class GoMerchantClaimResponse
{
    public string event_id;
    public string item_id;
    public long cost;
}

[Serializable]
internal sealed class GoItemListResponse
{
    public bool unlocked;
    public string day_key;
    public List<GoItemCatalogEntry> items = new List<GoItemCatalogEntry>();
    public List<string> owned_item_ids = new List<string>();
    public List<GoItemLoadoutEntry> loadout = new List<GoItemLoadoutEntry>();
}

[Serializable]
internal sealed class GoItemCatalogEntry
{
    public string item_id;
    public string rarity;
    public int price_gold;
    public string slot_type;
    public string effect_type;
    public string content_version;
}

[Serializable]
internal sealed class GoItemLoadoutEntry
{
    public string slot_type;
    public int slot;
    public string item_id;
    public string rarity;
    public int price_gold;
    public string effect_type;
    public string content_version;
    public string digest;
}

[Serializable]
internal sealed class GoItemLoadoutRequest
{
    public List<string> active_item_ids = new List<string>();
    public List<string> passive_item_ids = new List<string>();
}

[Serializable]
internal sealed class GoItemLoadoutResponse
{
    public string day_key;
    public List<GoItemLoadoutEntry> loadout = new List<GoItemLoadoutEntry>();
}

[Serializable]
internal sealed class GoSettings
{
    public float music_volume;
    public float sfx_volume;
    public string language;
}

[Serializable]
internal sealed class GoSavePushPreferences
{
    public bool energy_full;
    public bool events;
    public bool leaderboard_end;
    public bool comeback;
}

[Serializable]
internal sealed class GoSaveSnapshot
{
    public int save_schema_version;
    public string content_version;
    public long server_revision;
    public GoSettings settings;
    public GoSavePushPreferences push_preferences;
}

[Serializable]
internal sealed class GoSaveUpdate
{
    public long expected_revision;
    public GoSettings settings;
    public GoSavePushPreferences push_preferences;
}

[Serializable]
internal sealed class GoPushDeviceRequest
{
    public string device_id;
    public string platform;
    public string push_token;
    public string locale;
    public string iana_timezone;
}

[Serializable]
internal sealed class GoPushDeviceResponse
{
    public string device_id;
    public string platform;
    public string locale;
    public string iana_timezone;
    public bool authorized;
    public string updated_at;
}

[Serializable]
internal sealed class GoPushPreferencesResponse : IUnaryRawJsonResponse
{
    public bool energy_full;
    public bool events;
    public bool leaderboard_end;
    public bool comeback;
    public int? quiet_start_hour;
    public int? quiet_end_hour;

    public void ReadJson(string json)
    {
        GoPushPreferencesJson baseValue = UnityEngine.JsonUtility.FromJson<GoPushPreferencesJson>(json);
        if (baseValue == null) throw new FormatException("Push preferences response is empty.");
        energy_full = baseValue.energy_full;
        events = baseValue.events;
        leaderboard_end = baseValue.leaderboard_end;
        comeback = baseValue.comeback;
        quiet_start_hour = ReadNullableHour(json, "quiet_start_hour");
        quiet_end_hour = ReadNullableHour(json, "quiet_end_hour");
    }

    private static int? ReadNullableHour(string json, string field)
    {
        Match match = Regex.Match(
            json ?? string.Empty,
            "\\\"" + field + "\\\"\\s*:\\s*(null|[0-9]+)",
            RegexOptions.CultureInvariant);
        if (!match.Success || match.Groups[1].Value == "null") return null;
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out int value) || value < 0 || value > 23)
            throw new FormatException("Push quiet hour is invalid.");
        return value;
    }
}

[Serializable]
internal sealed class GoPushPreferencesJson
{
    public bool energy_full;
    public bool events;
    public bool leaderboard_end;
    public bool comeback;
}

internal sealed class GoPushPreferencesRequest : IUnaryRawJsonRequest
{
    public bool energy_full;
    public bool events;
    public bool leaderboard_end;
    public bool comeback;
    public int? quiet_start_hour;
    public int? quiet_end_hour;

    public string ToJson()
    {
        return "{\"energy_full\":" + Bool(energy_full) +
               ",\"events\":" + Bool(events) +
               ",\"leaderboard_end\":" + Bool(leaderboard_end) +
               ",\"comeback\":" + Bool(comeback) +
               ",\"quiet_start_hour\":" + Hour(quiet_start_hour) +
               ",\"quiet_end_hour\":" + Hour(quiet_end_hour) + "}";
    }

    private static string Bool(bool value) => value ? "true" : "false";
    private static string Hour(int? value) => value.HasValue
        ? value.Value.ToString(CultureInfo.InvariantCulture)
        : "null";
}

[Serializable]
internal sealed class GoSocialLinkRequest
{
    public string authorization_code;
}

[Serializable]
internal sealed class GoSocialLinkResponse
{
    public string link_id;
    public string provider;
    public string linked_at;
}

[Serializable]
internal sealed class GoSocialSyncRequest
{
    public string provider;
}

[Serializable]
internal sealed class GoSocialFriend
{
    public string player_id;
    public string provider;
}

[Serializable]
internal sealed class GoSocialFriendsResponse
{
    public List<GoSocialFriend> friends = new List<GoSocialFriend>();
}

[Serializable]
internal sealed class GoSocialSyncResponse
{
    public string provider;
    public int synced_count;
    public List<GoSocialFriend> friends = new List<GoSocialFriend>();
}

[Serializable]
internal sealed class GoShareStatusResponse
{
    public string day_key;
    public int created_count;
    public int verified_count;
    public int claimed_count;
    public int remaining;
}

[Serializable]
internal sealed class GoShareCreateRequest
{
    public string platform;
    public string placement_id;
    public bool client_share_succeeded;
    public string client_share_event_id;
}

[Serializable]
internal sealed class GoShareCreateResponse
{
    public string share_id;
    public string share_type;
    public string platform;
    public string share_token;
    public string created_at;
}

[Serializable]
internal sealed class GoShareClaimRequest
{
    public string share_id;
    public string share_token;
    public string platform;
    public string placement_id;
    public bool client_share_succeeded;
    public string client_share_event_id;
}

[Serializable]
internal sealed class GoShareClaimResponse
{
    public int reward_amount;
    public GoEnergySnapshot energy;
}

[Serializable]
internal sealed class GoClientReportedAdPayload
{
    public string reward_scope;
}

[Serializable]
internal sealed class GoAdClaimRequest
{
    public string transaction_id;
    public string player_id;
    public string placement;
    public string provider;
    public GoClientReportedAdPayload payload;
    public string signature;
    public string run_id;
    public bool client_ad_succeeded;
    public string client_ad_event_id;
    public string ad_platform;
}

[Serializable]
internal sealed class GoAdClaimResponse
{
    public bool replayed;
}

[Serializable]
internal sealed class GoSigninReward
{
    public string type;
    public string id;
    public string rarity;
    public long amount;
}

[Serializable]
internal sealed class GoSigninStatusResponse
{
    public string day_key;
    public int cycle_day;
    public int current_streak;
    public int total_signins;
    public bool can_claim;
    public bool double_available;
    public List<GoSigninReward> preview_rewards = new List<GoSigninReward>();
}

[Serializable]
internal sealed class GoSigninClaimRequest
{
    public string expected_day_key;
    public string claim_mode;
    public bool client_share_succeeded;
    public string client_share_event_id;
    public string share_platform;
}

[Serializable]
internal sealed class GoSigninClaimResponse
{
    public long claim_id;
    public string day_key;
    public int cycle_day;
    public int current_streak;
    public int total_signins;
    public string claim_mode;
    public bool applied;
    public bool replayed;
    public GoSigninReward reward;
    public List<GoSigninReward> rewards = new List<GoSigninReward>();
}
