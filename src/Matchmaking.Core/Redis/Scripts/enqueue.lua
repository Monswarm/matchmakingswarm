-- KEYS[1] tickets hash, KEYS[2] region queue by mmr, KEYS[3] region queue by enqueue time,
-- KEYS[4] previous assignment of the player
-- ARGV[1] player id, ARGV[2] region, ARGV[3] mmr, ARGV[4] now (unix ms)
if redis.call('HEXISTS', KEYS[1], ARGV[1]) == 1 then
    return 0
end

redis.call('DEL', KEYS[4])
redis.call('HSET', KEYS[1], ARGV[1], ARGV[2] .. '|' .. ARGV[3] .. '|' .. ARGV[4])
redis.call('ZADD', KEYS[2], ARGV[3], ARGV[1])
redis.call('ZADD', KEYS[3], ARGV[4], ARGV[1])
return 1
