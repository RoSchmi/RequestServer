#include "Updater.h"

#include <algorithm>
#include <functional>

using namespace std;
using namespace util;
using namespace game_server;
using namespace game_server::objects;

updater::updater(word updates_per_tick, chrono::microseconds sleep_for) : timer(sleep_for) {
	this->updates_per_tick = updates_per_tick;
	this->position = 0;
	this->timer.on_tick += bind(&updater::tick, this);
}

updater::~updater() {

}

void updater::tick() {
	unique_lock<mutex> lck(this->lock);

	if (this->position >= this->objects.size())
		this->position = 0;

	for (word i = 0; i < this->updates_per_tick && i + this->position < this->objects.size(); i++) {
		auto now = date_time::clock::now();
		int64 delta = chrono::duration_cast<chrono::milliseconds>(now - this->objects[i]->last_updated).count();
		this->objects[i]->update(static_cast<uint64>(delta));
		this->objects[i]->last_updated = now;
	}
}

void updater::add(updatable* object) {
	unique_lock<mutex> lck(this->lock);
	this->objects.push_back(object);
}

void updater::remove(updatable* object) {
	unique_lock<mutex> lck(this->lock);
	auto iter = find(this->objects.begin(), this->objects.end(), object);
	this->objects.erase(iter);
}

cache_updater::cache_updater(cache_provider& cache, word updates_per_tick, chrono::microseconds sleep_for) : cache(cache), timer(sleep_for) {
	this->updates_per_tick = updates_per_tick;
	this->position = 0;
	this->timer.on_tick += bind(&cache_updater::tick, this);
}

cache_updater::~cache_updater() {

}

void cache_updater::tick() {
	unique_lock<cache_provider> lck(this->cache);

	for (word i = 0; i < this->updates_per_tick; i++) {
		updatable* object = this->cache.get_next_updatable(this->position);
		if (!object)
			return;

		auto now = date_time::clock::now();
		int64 delta = chrono::duration_cast<chrono::milliseconds>(now - object->last_updated).count();
		object->update(static_cast<uint64>(delta));
		object->last_updated = now;
	}
}
